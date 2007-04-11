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
	lock(_node.ConnectionTable.SyncRoot) {
	  ArrayList nearest = _node.ConnectionTable.GetNearestTo(
								 (AHAddress) _node.Address, 6);
	  ArrayList tas = new ArrayList();
	  foreach(Connection cons in nearest) {
#if TUNNEL_DEBUG
	    Console.Error.WriteLine("TunnelEdgeListener: testing if we can tunnel using connection: {0}", cons.Address);
#endif
	    if (cons.Edge.TAType != TransportAddress.TAType.Tunnel) {
	      TunnelTransportAddress tun_ta = new TunnelTransportAddress(_node.Address, cons.Address);
	      tas.Add(tun_ta);
#if TUNNEL_DEBUG
	      Console.Error.WriteLine("TunnelEdgeListener: added tunnel TA: {0}", tun_ta);
#endif
	      //atmost 4 TAs are added
	      if (tas.Count >= 4) {
		break;
	      }
	    } 
	  }
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
    
    protected bool _running;
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
	Connection forwarding_con = null;
	try {
        lock(_node.ConnectionTable.SyncRoot) {
	  Console.Error.WriteLine("TunnelEdgeListener: Retrieving list of structured connections");
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

	    TunnelTransportAddress test_ta = new TunnelTransportAddress(tun_ta.Target, con.Address);
	    //Console.Error.WriteLine("comparing tun_ta: {0}", tun_ta);
	    //Console.Error.WriteLine("comparing test_ta: {0}", test_ta);
	    if (!test_ta.Equals(tun_ta)) {
	      Console.Error.WriteLine("Cannot tunnel over connection: " + con.Address.ToString());
	      continue;
	    }
#if TUNNEL_DEBUG
	    Console.Error.WriteLine("Can tunnel over connection: " + con.Address.ToString());
#endif
	    forwarding_con = con;
	    break;
	  }
	}
	} catch(Exception e) {
	  Console.Error.WriteLine(e);
	}
	if (forwarding_con == null) {
	  ecb(false, null, new EdgeException("Cannot create edge over TA: " + tun_ta));
	  return;
	}
	ForwardingSender fs = new ForwardingSender(_node, forwarding_con.Address, 1);

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
				      forwarding_con.Address, localid, remoteid, new byte[ 1 + 8 + Packet.MaxLength ]);
#if TUNNEL_DEBUG
	  Console.Error.WriteLine("Creating an instance of TunnelEdge: {0}", e);
	  Console.Error.WriteLine("remoteid: {0}, localid: {1}", remoteid, localid);
#endif      
	  _id_ht[localid] = e;
	  //we will defer the new edge event for later
	  //when we actually get a response
	  
	  //now build a packet payload
	  byte[] payload = new byte[Address.MemSize + 9];
	  
	  
	  payload[0] = (byte) MessageType.EdgeRequest;
	  NumberSerializer.WriteInt(localid, payload, 1);
	  NumberSerializer.WriteInt(remoteid, payload, 5);
	  
	  //we copy our own address into the payload
	  _node.Address.CopyTo(payload, 9);
	  
	  p = new AHPacket(0, 1, _node.Address, tun_ta.Target, AHPacket.AHOptions.Exact, 
			   AHPacket.Protocol.Tunneling, payload);
	  EdgeCreationState ecs = new EdgeCreationState();
	  ecs.Id = localid;
	  ecs.ECB = ecb;
	  ecs.RequestPacket = p;
	  ecs.Sender = fs;
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
      public ForwardingSender Sender;
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
	ecs.Sender.Send(ecs.RequestPacket);
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
      MemBlock mb = packet.Payload;
      //read the payload?
      MessageType type = (MessageType) mb[0];
      int remoteid = NumberSerializer.ReadInt(mb, 1);
      int localid = NumberSerializer.ReadInt(mb, 5);

#if TUNNEL_DEBUG
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
	AHAddress target = new AHAddress(mb.Slice(9));

	//it is however possible that we have already created the edge locally
	TunnelEdge e_dup = (TunnelEdge) _remote_id_ht[remoteid];
	if (e_dup != null) {
	  TunnelTransportAddress remote_ta = new TunnelTransportAddress(target, packet.Source);	  
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
	  TunnelEdge e = new TunnelEdge(this, true, _node, target, packet.Source, localid, 
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
	byte[] payload = new byte[Address.MemSize + 9];
	payload[0] = (byte) MessageType.EdgeResponse;
	NumberSerializer.WriteInt(localid, payload, 1);
	NumberSerializer.WriteInt(remoteid, payload, 5);
	
	//we copy out own addres into the edge response
	_node.Address.CopyTo(payload, 9);
	  
	Packet p = new AHPacket(0, 1, _node.Address, target, AHPacket.AHOptions.Exact, 
				AHPacket.Protocol.Tunneling, payload);
	ForwardingSender fs = new ForwardingSender(_node, packet.Source, 1);
#if TUNNEL_DEBUG
	Console.Error.WriteLine("Sending edge response: {0}", p);
#endif      
	
	fs.Send(p);
      } 
      else if (type == MessageType.EdgeResponse) { //EdgeResponse
	//assert (localid > 0) 
#if TUNNEL_DEBUG
	Console.Error.WriteLine("Receiving edge response: {0}", packet);
#endif
	//unlikely to be a new edge
	is_new_edge = false;
	AHAddress target = null;


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
	  target = new AHAddress(mb.Slice(9));
	  TunnelTransportAddress remote_ta = new TunnelTransportAddress(target, packet.Source);
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
      } else {//type == MessageType.EdgeData
#if TUNNEL_DEBUG
	Console.Error.WriteLine("Receiving edge data");
#endif
	TunnelEdge edge_to_read = (TunnelEdge) _id_ht[localid];
	if (edge_to_read != null) {
	  if (edge_to_read.RemoteID == remoteid) {
	    try {
#if TUNNEL_DEBUG
	      Console.Error.WriteLine("Receiving data on edge: {0}", edge_to_read);
#endif
	      Packet p = PacketParser.Parse(mb.Slice(9));
	      edge_to_read.Push(p);
	    } catch(ParseException pe) {
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
      }
      } //lock ( _sync )
    }

    public void HandleEdgeSend(Edge e, ICopyable packet) {
      if (!_running) {
	//do nothing
	return;
      }
      lock(e) {
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
	if (tun_edge.PacketSender != null) {
	  tun_edge.PacketSender.Send(p);
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
