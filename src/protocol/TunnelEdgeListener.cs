/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, Arijit Ganguly <aganguly@acis.ufl.edu>
University of Florida
This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using Brunet;
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.IO;

namespace Brunet
{

  /**
   * This class implements the TunnelEdgeListener. Sometime due to NATs or some BGP 
   * outage, its possible two neighboring nodes in P2P cannot connect. In such a situation,
   * we create an edge that tunnel packets through some other P2P node. 
   */

  public class TunnelEdgeListener: EdgeListener, IAHPacketHandler, IEdgeSendHandler
  {
    /**
     * A ReadOnly list of TransportAddress objects for
     * this EdgeListener
     */
    public override IEnumerable LocalTAs
    {
      get {
	ArrayList tas = new ArrayList();
	lock(_node.ConnectionTable.SyncRoot) {
	  ArrayList nearest = _node.ConnectionTable.GetNearestTo(
								 (AHAddress) _node.Address, 6);
	  ArrayList forwarders = new ArrayList();
	  foreach(Connection cons in nearest) {
#if TUNNEL_DEBUG
	    Console.Error.WriteLine("TunnelEdgeListener: testing if we can tunnel using node: {0}", cons.Address);
#endif
	    if (cons.Edge.TAType != TransportAddress.TAType.Tunnel) {
	      forwarders.Add(cons.Address);
#if TUNNEL_DEBUG
	      Console.Error.WriteLine("TunnelEdgeListener: added node: {0} to tunnel TA", cons.Address);
#endif
	    }
	  }
	  if (forwarders.Count < 2) {
	    //we should have atleast 1 forwarders
	    return tas;
	  }
	  TunnelTransportAddress tun_ta = new TunnelTransportAddress(_node.Address, forwarders);
#if TUNNEL_DEBUG
	  Console.Error.WriteLine("TunnelEdgeListener: built tunnel TA: {0}", tun_ta);
#endif	  
	  tas.Add(tun_ta);
	  return tas;
	}
      }
    }

      /**
       * What type of TransportAddress does this EdgeListener use
       */
    public override Brunet.TransportAddress.TAType TAType
    {
      get {
	return TransportAddress.TAType.Tunnel;
      }
    }

    //all packet receiving happens through the node
    protected Node _node;

    /**
     *  Hashtable of ID to Edges
     */
    protected Hashtable _id_ht;
    protected Hashtable _remote_id_ht;

    protected Random _rand;

    protected object _sync;
    
    volatile protected bool _running;
    protected bool _isstarted;

    /**
     * @return true if the Start method has been called
     */
    public override bool IsStarted
    {
      get {
	return _isstarted;
      }
    }
    public enum MessageType:byte 
    {
      EdgeRequest,
      EdgeResponse,
      EdgeData,
      EdgeControl,
    }

    /**
     * @param ta TransportAddress to create an edge to
     * @param ecb the EdgeCreationCallback to call when done
     * @throw EdgeException if we try to call this before calling
     * Start.
     */
    public override void CreateEdgeTo(TransportAddress ta, EdgeCreationCallback ecb) 
    {
      if (!IsStarted) {
	ecb(false, null, new EdgeException("TunnelEdgeListener not started"));
      }
      else if (!_running) {
	ecb(false, null, new EdgeException("TunnelEdgeListener not running"));	
      }
      else if (ta.TransportAddressType != this.TAType) {
	ecb(false, null,
            new EdgeException(ta.TransportAddressType.ToString()
                              + " is not my type: " + this.TAType.ToString() ) );
      }
      else {
#if TUNNEL_DEBUG
	Console.Error.WriteLine("CreateEdgeTo TunnelEdge to: {0}", ta);
#endif  
	TunnelTransportAddress tun_ta = ta as TunnelTransportAddress;
	ArrayList forwarders = new ArrayList();
	ArrayList forwarding_edges = new ArrayList();
	lock(_node.ConnectionTable.SyncRoot) {
	  Console.Error.WriteLine("TunnelEdgeListener: Finding structured connections to tunnel over");
	  IEnumerable struc_cons = _node.ConnectionTable.GetConnections(ConnectionType.Structured);
	  if (struc_cons ==  null) {
	    Console.Error.WriteLine("List of structured connections is null");
	  }
	  Console.Error.WriteLine("TunnelEdgeListener: Browsing list of structured connections");
	  foreach (Connection con in struc_cons) {
	    Console.Error.WriteLine("TunnelEdgeListener: Testing : {0}", con.Address);
	    if (con.Edge.TAType == TransportAddress.TAType.Tunnel) {
	      Console.Error.WriteLine("Cannot tunnel over tunnel: " + con.Address.ToString());
	      continue;
	    }
	    if (!tun_ta.ContainsForwarder(con.Address)) {
	      Console.Error.WriteLine("Cannot tunnel over connection: " + con.Address.ToString());
	      continue;
	    }
#if TUNNEL_DEBUG
	    Console.Error.WriteLine("Can tunnel over connection: " + con.Address.ToString());
#endif
	    forwarders.Add(con.Address);
	    forwarding_edges.Add(con.Edge);
	  }
	}

	if (forwarders.Count < 2) {
	  ecb(false, null, new EdgeException("Cannot create edge over TA: " + tun_ta + ", not many forwarders"));
	  return;
	}
	tun_ta = new TunnelTransportAddress(tun_ta.Target, forwarders);
	
	//choose a locally unique id
	int localid;
	int remoteid = 0;
	Packet p = null;
	lock( _sync ) {
	  //Get a random ID for this edge:
	  do {
	    localid = _rand.Next();
	    //Make sure we don't have negative ids
	    if( localid < 0 ) { localid = ~localid; }
	  } while( _id_ht.Contains(localid) || localid == 0 );      
	  //looks like the new edge is ready
	  TunnelEdge e = new TunnelEdge(this, false, _node, tun_ta.Target,
					forwarders, localid, remoteid, new byte[ 1 + 8 + Packet.MaxLength ]);
#if TUNNEL_DEBUG
	  Console.Error.WriteLine("Creating an instance of TunnelEdge: {0}", e);
	  Console.Error.WriteLine("remoteid: {0}, localid: {1}", remoteid, localid);
#endif      
	  _id_ht[localid] = e;
	  //we will defer the new edge event for later
	  //when we actually get a response
	  
	  //now build the packet payload
	  MemoryStream ms = new MemoryStream();
	  ms.WriteByte((byte) MessageType.EdgeRequest);
	  NumberSerializer.WriteInt(localid, ms);
	  NumberSerializer.WriteInt(remoteid, ms);
	  Console.Error.WriteLine("Written off type, localid, remoteid");
	  
	  ArrayList args = new ArrayList();
	  //add the target address
	  byte[] addr_bytes = new byte[Address.MemSize];
	  _node.Address.CopyTo(addr_bytes);
	  args.Add(addr_bytes.Clone());
	  Console.Error.WriteLine("Added target address");
	  
	  foreach (Address fwd in  forwarders) {
	    //add forwarding addresses
	    fwd.CopyTo(addr_bytes);
	    args.Add(addr_bytes.Clone());
	    Console.Error.WriteLine("Added a forwarding address");

	  }
	  Console.Error.WriteLine("Creating a memory stream holding the payload");
	  AdrConverter.Serialize(args, ms);
	  p = new AHPacket(0, 1, _node.Address, tun_ta.Target, AHPacket.AHOptions.Exact, 
			   AHPacket.Protocol.Tunneling, ms.ToArray());
	  Console.Error.WriteLine("Created a request packet.");
	  EdgeCreationState ecs = new EdgeCreationState();
	  ecs.Id = localid;
	  ecs.ECB = ecb;
	  ecs.RequestPacket = p;
	  //all these edges can be used to send out packets for this tunnel edge
	  ecs.Senders = forwarding_edges;
	  _ecs_ht[localid] = ecs;

#if TUNNEL_DEBUG
	  Console.Error.WriteLine("Created an edge creation state for the tunnel edge: {0}", e);
#endif
	}
      }
      //we will defer this sending to next heartbeat; an artificial delay from out own side
    }


    protected class EdgeCreationState {
      public int Id;
      public EdgeCreationCallback ECB;
      public Packet RequestPacket;
      public ArrayList Senders;
      public int Attempts = 3;
    }
    protected Hashtable _ecs_ht;
    protected DateTime _last_check;
    protected TimeSpan _reqtimeout;

    protected void TimeoutChecker(object o, EventArgs args) {
      DateTime now = DateTime.UtcNow;
      if ( now - _last_check < _reqtimeout ) {
	return;
      }
      _last_check = now;


#if TUNNEL_DEBUG
      Console.Error.WriteLine("TimeoutChecker: Checking edge creation states at: {0}.", DateTime.Now);
#endif
      ArrayList to_remove = new ArrayList();
      ArrayList to_send = new ArrayList();
      lock (_sync) {
	IDictionaryEnumerator ide = _ecs_ht.GetEnumerator();
	while(ide.MoveNext()) {
	  //check the status of corresponding edge
	  int id = (int) ide.Key;
	  EdgeCreationState ecs = (EdgeCreationState) ide.Value;
	  if (ecs == null) {
	    Console.Error.WriteLine("This is wierd. How can ECS be null?");
	  }
	  TunnelEdge e = (TunnelEdge) _id_ht[id];
	  
	  if (e == null) 
	  {
#if TUNNEL_DEBUG
	    Console.Error.WriteLine("TimeoutChecker: removing ECS (localid: {0}) for null edge. ", ecs.Id);
#endif
	    to_remove.Add(ecs);
	  }
	  else if (e.RemoteID > 0) {
#if TUNNEL_DEBUG
	    Console.Error.WriteLine("TimeoutChecker: removing ECS (complete edge localid: {0}) for: {1}", ecs.Id, e);
#endif
	    to_remove.Add(ecs);
	  }
	  else if (ecs.Attempts <= 0) {
#if TUNNEL_DEBUG
	    Console.Error.WriteLine("TimeoutChecker: removing ECS (timed out local id: {0}) for: {1}", ecs.Id, e);
#endif
	    to_remove.Add(ecs);
	  }
	  else if (ecs.Attempts > 0) {
	    to_send.Add(ecs);
	  }
	}
	foreach (EdgeCreationState ecs in to_remove) {
	  _ecs_ht.Remove(ecs.Id);
	  
	  //also remove this edge from the list
	  _id_ht.Remove(ecs.Id);
	  
	  ecs.ECB(false, null, new Exception("Timed out on edge creation."));
	}
      }
      //the following can happen outside the lock
      foreach (EdgeCreationState ecs in to_send) {
	ecs.Attempts--;
#if TUNNEL_DEBUG
	Console.Error.WriteLine("Sending edge (localid: {0}) request: {1}", ecs.Id, ecs.RequestPacket);
#endif
	Edge e = (Edge) ecs.Senders[_rand.Next(0, ecs.Senders.Count)];
	try {
	  e.Send(ecs.RequestPacket);
	} catch(Exception ex) {
	  Console.Error.WriteLine(ex);
	}
      }
    }

    /**
     * Start listening for edges.  Edges
     * received will be announced with the EdgeEvent
     * 
     * This must be called before CreateEdgeTo.
     */
    public override void Start() {
      lock( _sync ) {
        if( _isstarted ) {
          //We can't start twice... too bad, so sad:
          throw new Exception("Restart never allowed");
        }
#if TUNNEL_DEBUG
	Console.Error.WriteLine("Starting TunnelEdgeListener");
#endif
        _isstarted = true;
        _running = true;
      }
    }
    /**
     * Stop listening for edges.
     * The edgelistener may not be garbage collected
     * until this is called
     */
    public override void Stop() {
      lock(_sync) {
	_running = false;
	_node.HeartBeatEvent -= new EventHandler(TimeoutChecker);
	_node.Unsubscribe(AHPacket.Protocol.Tunneling, this);      
      }
    }
    
    public TunnelEdgeListener(Node n) {
      _sync = new object();
#if TUNNEL_DEBUG
      Console.Error.WriteLine("Creating an instance of TunnelEdgeListsner");
#endif
      lock(_sync) {
	_node = n;
	_node.Subscribe(AHPacket.Protocol.Tunneling, this);
	
	//true for now, will change later
	_ta_auth = new ConstantAuthorizer(TAAuthorizer.Decision.Allow);
	

	_id_ht = new Hashtable(30, 0.15f);
	_remote_id_ht = new Hashtable();
	_rand = new Random();
	_ecs_ht = new Hashtable();
	

	_running = false;
	_isstarted = false;
	
	_last_check = DateTime.Now;
	//resend the request after 5 seconds.
	_reqtimeout = new TimeSpan(0,0,0,0,5000);

	_node.HeartBeatEvent += new EventHandler(this.TimeoutChecker);


      }
    }
    public void HandleAHPacket(object node, AHPacket packet, Edge from)
    {
      if (!_running) {
	Console.Error.WriteLine("TunnelEdgeListeber: not running (cannot handle packet)");
	return;
      }
      MemoryStream payload_ms = packet.PayloadStream;
      //read the payload?
      MessageType type = (MessageType) payload_ms.ReadByte();
      int remoteid = NumberSerializer.ReadInt(payload_ms);
      int localid = NumberSerializer.ReadInt(payload_ms);
      
#if TUNNEL_DEBUG
      Console.Error.WriteLine("TunnelEdgeListeber: Receiving on base connection: {0}", _node.ConnectionTable.GetConnection(from));
      Console.Error.WriteLine("Receiving edge packet, remoteid: {0}, localid: {1}", remoteid, localid);
#endif

      
      bool is_new_edge = false;

      lock(_sync) {
     //in case this is an incomig edge request
      if (type == MessageType.EdgeRequest) {
#if TUNNEL_DEBUG
	Console.Error.WriteLine("Receiving edge request: {0}", packet);
#endif
	//assert (localid == 0)
	//probably a new incoming edge
	is_new_edge = true;
	ArrayList args = (ArrayList) AdrConverter.Deserialize(payload_ms);
	Address target = new AHAddress(MemBlock.Copy((byte[]) args[0]));
	//list of packet forwarders
	ArrayList forwarders = new ArrayList();
	for (int i = 1; i < args.Count; i++) {
	  forwarders.Add(new AHAddress(MemBlock.Copy((byte[]) args[i])));
	}
	//it is however possible that we have already created the edge locally
	TunnelEdge e_dup = (TunnelEdge) _remote_id_ht[remoteid];
	if (e_dup != null) {
	  TunnelTransportAddress remote_ta = new TunnelTransportAddress(target, forwarders);	  
	  //compare TAs
	  if (e_dup.RemoteTA.Equals(remote_ta)) {
	    //the fellow sent a duplicate edge request
	    is_new_edge = false;
#if TUNNEL_DEBUG
	    Console.Error.WriteLine("Duplicate edge request: from {0}", remote_ta);
#endif
	    //but do send a response back
	    //we also have to send a response back now
	  } else {
	    //someone else guessed the same id on its side
	    //still okay, we can generate a unqiue id locally
	  }
	} else {
	  //this is the first edge request from a node and also
	  //has a unique id on its side
	  
	}
	if(is_new_edge) {
	  do {
	    localid = _rand.Next();
	    //Make sure not to use negative ids
	    if( localid < 0 ) { localid = ~localid; }
	  } while( _id_ht.Contains(localid) || localid == 0 );
	  
	  //create an edge
	  TunnelEdge e = new TunnelEdge(this, true, _node, target, forwarders, localid, 
					remoteid, new byte[ 1 + 8 + Packet.MaxLength ]);
#if TUNNEL_DEBUG
	  Console.Error.WriteLine("Creating an instance of TunnelEdge: {0}", e);
	  Console.Error.WriteLine("remoteid: {0}, localid: {1}", remoteid, localid);
#endif      

	  _id_ht[localid] = e;
	  _remote_id_ht[remoteid] = e;
	  e.CloseEvent += new EventHandler(this.CloseHandler);
	  Console.Error.WriteLine("announcing tunnel edge (incoming)");
	  SendEdgeEvent(e);
	}

	//we also have to send a response back now
	MemoryStream ms = new MemoryStream();
	ms.WriteByte((byte) MessageType.EdgeResponse);
	NumberSerializer.WriteInt(localid, ms);
	NumberSerializer.WriteInt(remoteid, ms);
	
	//overwrite the first address in the edge response
	_node.Address.CopyTo((byte[]) args[0]);

	AdrConverter.Serialize(args, ms);
	Packet p = new AHPacket(0, 1, _node.Address, target, AHPacket.AHOptions.Exact, 
				AHPacket.Protocol.Tunneling, ms.ToArray());
	//send using the edge we received data on
#if TUNNEL_DEBUG
	Console.Error.WriteLine("Sending edge response: {0}", p);
#endif
	try {
	  from.Send(p);
	} catch (Exception ex) {
	  Console.Error.WriteLine(ex);
	}
      }
      else if (type == MessageType.EdgeResponse) { //EdgeResponse
	//assert (localid > 0) 
#if TUNNEL_DEBUG
	Console.Error.WriteLine("Receiving edge response: {0}", packet);
#endif
	//unlikely to be a new edge
	is_new_edge = false;

	ArrayList args = (ArrayList) AdrConverter.Deserialize(payload_ms);
	Address target = new AHAddress(MemBlock.Copy((byte[]) args[0]));
	//list of packet forwarders
	ArrayList forwarders = new ArrayList();
	for (int i = 1; i < args.Count; i++) {
	  forwarders.Add(new AHAddress(MemBlock.Copy((byte[]) args[i])));
	}
	
	TunnelEdge e = (TunnelEdge) _id_ht[localid];
	if (e == null) {
	  //this is strange
	} 
	else if (e.RemoteID == 0) {
#if TUNNEL_DEBUG
	  Console.Error.WriteLine("Must verify the remoteTA for the response: {0}", packet);
#endif
	  //possible response to our create edge request, make sure this 
	  //is the case by verifying the remote TA
	  
	  TunnelTransportAddress remote_ta = new TunnelTransportAddress(target, forwarders);
#if TUNNEL_DEBUG
	  Console.Error.WriteLine("response.RemoteTA: {0}", remote_ta);
	  Console.Error.WriteLine("edge.RemotTA: {0}", e.RemoteTA);
#endif
	  if (e.RemoteTA.Equals(remote_ta)) {
	    e.RemoteID = remoteid;
#if TUNNEL_DEBUG
	    Console.Error.WriteLine("Edge protocol complete: {0}", e);
#endif
	    //raise an edge creation event 
	    //this was an outgoing edge
	    is_new_edge = true;
	    
	  } else {
	    //remote TAs do not match (ignore)
	  } 
	}
	else if (e.RemoteID != remoteid) {
	  //we simply ignore this
	}
	if (is_new_edge) {
	  //this would be an outgoing edge
#if TUNNEL_DEBUG
	  Console.Error.WriteLine("remoteid: {0}, localid: {1}", remoteid, localid);
#endif      
	  e.CloseEvent += new EventHandler(this.CloseHandler);

	  EdgeCreationState ecs = (EdgeCreationState) _ecs_ht[localid];
	  _ecs_ht.Remove(localid);

	  //ecs.ECB(false, null, new Exception("something which i just made up"));
	  Console.Error.WriteLine("announcing tunnel edge (outgoing)");
	  ecs.ECB(true, e, null);

	}
      } else if(type == MessageType.EdgeData) {
#if TUNNEL_DEBUG
	Console.Error.WriteLine("Receiving edge data");
#endif
	TunnelEdge edge_to_read = (TunnelEdge) _id_ht[localid];
	if (edge_to_read != null) {
	  if (edge_to_read.RemoteID == remoteid) {
	    try {
	      Packet p = PacketParser.Parse(MemBlock.Reference(payload_ms.ToArray()).Slice((int)payload_ms.Position));
	      edge_to_read.Push(p);
#if TUNNEL_DEBUG
	      Console.Error.WriteLine("Receiving packet of length: {0} on edge: {1}", p.Length, edge_to_read);
#endif
	    } catch(Exception pe) {
	      System.Console.Error.WriteLine("Edge: {0} sent us an unparsable packet: {1}", 
					     edge_to_read, pe);
	    }
	  } else {
#if TUNNEL_DEBUG
	    Console.Error.WriteLine("No correspondig edge to push packet into (Id mismatch).");
#endif
	  }
	} else {
#if TUNNEL_DEBUG
	  Console.Error.WriteLine("No correspondig edge to push packet into (null edge).");
#endif
	}
      } else if (type == MessageType.EdgeControl) {
#if TUNNEL_DEBUG
	Console.Error.WriteLine("Receiving edge control");
#endif
	TunnelEdge tun_edge = (TunnelEdge) _id_ht[localid];	
	if (tun_edge != null) {
	  if (tun_edge.RemoteID == remoteid) {
	    ArrayList arg1 = (ArrayList) AdrConverter.Deserialize(payload_ms);
	    //list of acquired forwarders
	    ArrayList acquired = new ArrayList();
	    for (int i = 0; i < arg1.Count; i++) {
	      acquired.Add(new AHAddress(MemBlock.Copy((byte[]) arg1[i])));
	    }
	    ArrayList arg2 = (ArrayList) AdrConverter.Deserialize(payload_ms);
	    //list of lost forwarders
	    ArrayList lost = new ArrayList();
	    for (int i = 0; i < arg2.Count; i++) {
	      lost.Add(new AHAddress(MemBlock.Copy((byte[]) arg2[i])));
	    }	    

	    tun_edge.HandleControlPacket(acquired, lost);
	  } else {
#if TUNNEL_DEBUG
	    Console.Error.WriteLine("No correspondig edge to push packet into (Id mismatch).");
#endif
	  }
	} else {
#if TUNNEL_DEBUG
	  Console.Error.WriteLine("No correspondig edge to push packet into (null edge).");
#endif
	}
      }
      } //lock ( _sync )
    }

    public void HandleControlSend(Edge e, ArrayList acquired, ArrayList lost) {
      if (!_running) {
	//do nothing
	return;
      }
      TunnelEdge tun_edge = e as TunnelEdge;

      MemoryStream ms = new MemoryStream();
      ms.WriteByte((byte) MessageType.EdgeControl);

      NumberSerializer.WriteInt(tun_edge.ID, ms);
      NumberSerializer.WriteInt(tun_edge.RemoteID, ms);
      
      //write out newly acquired forwarders
      ArrayList arg1 = new ArrayList();
      byte[] addr_bytes = new byte[Address.MemSize];
      
      foreach (Address addr in acquired) {
	//add forwarding addresses
	addr.CopyTo(addr_bytes);
	arg1.Add(addr_bytes.Clone());
	Console.Error.WriteLine("Added a acquired address: {0}", addr);
      }

      //write out lost addresses
      ArrayList arg2 = new ArrayList();
      
      foreach (Address addr in lost) {
	//add forwarding addresses
	addr.CopyTo(addr_bytes);
	arg2.Add(addr_bytes.Clone());
	Console.Error.WriteLine("Added a lost address: {0}", addr);
      }      

      AdrConverter.Serialize(arg1, ms);
      AdrConverter.Serialize(arg2, ms);
      

      Packet p = new AHPacket(0, 1, _node.Address, tun_edge.Target, AHPacket.AHOptions.Exact, 
			      AHPacket.Protocol.Tunneling, ms.ToArray());
      
      if (tun_edge.PacketSenders.Count > 0) {
	IPacketSender sender = (IPacketSender) tun_edge.PacketSenders[_rand.Next(0, tun_edge.PacketSenders.Count)];
	try {
	  Console.Error.WriteLine("Sending control out on base connection: {0}", _node.ConnectionTable.GetConnection((Edge) sender));
	  sender.Send(p);
	} catch(Exception ex) {
#if TUNNEL_DEBUG	  
	  Console.Error.WriteLine("Error sending control using packet_sender: {0}, {1}", sender, ex);
#endif
	}       
      }
    }

    public void HandleEdgeSend(Edge e, ICopyable packet) {
      if (!_running) {
	//do nothing
	return;
      }
      TunnelEdge tun_edge = e as TunnelEdge;
      tun_edge.SendBuffer[0] = (byte) TunnelEdgeListener.MessageType.EdgeData;
      //Write the IDs of the edge:
      //[edge data][local id 4 bytes][remote id 4 bytes][packet]
      NumberSerializer.WriteInt(tun_edge.ID, tun_edge.SendBuffer, 1);
      NumberSerializer.WriteInt(tun_edge.RemoteID, tun_edge.SendBuffer, 5);
#if TUNNEL_DEBUG
      Console.Error.WriteLine("For data, tun_edge remoteID: {0}, localID: {1}", tun_edge.RemoteID, tun_edge.ID);
#endif
      packet.CopyTo(tun_edge.SendBuffer, 9);
      Packet p = new AHPacket(0, 1, _node.Address, tun_edge.Target, AHPacket.AHOptions.Exact,
			      AHPacket.Protocol.Tunneling, tun_edge.SendBuffer, 0, 9 + packet.Length);
      
      if (tun_edge.PacketSenders.Count > 0) {
	IPacketSender sender = (IPacketSender) tun_edge.PacketSenders[_rand.Next(0, tun_edge.PacketSenders.Count)];
	try {
	  Console.Error.WriteLine("Sending data out on base connection: {0}", _node.ConnectionTable.GetConnection((Edge) sender));
	  sender.Send(p);
	} catch(Exception ex) {
#if TUNNEL_DEBUG	  
	  Console.Error.WriteLine("Error sending using packet_sender: {0}, {1}", sender, ex);
#endif
	} 
      }
    }
    

    public bool HandlesAHProtocol(string type)
    {
      return (type == AHPacket.Protocol.Tunneling);
    }
    /*
     * When a UdpEdge closes we need to remove it from
     * our table, so we will know it is new if it comes
     * back.
     */
    public void CloseHandler(object edge, EventArgs args)
    {
      Console.Error.WriteLine("closing tunnel edge");
      TunnelEdge e = (TunnelEdge)edge;
      lock( _sync ) {
        _id_ht.Remove( e.ID );
	object re = _remote_id_ht[ e.RemoteID ];
	if( re == e ) {
          //_remote_id_ht only keeps track of incoming edges,
	  //so, there could be two edges with the same remoteid
	  //that are not equivalent.
	  _remote_id_ht.Remove( e.RemoteID );
	}
      }
    }
  }
}
