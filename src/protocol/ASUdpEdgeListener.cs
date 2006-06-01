/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2005,2006  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

/*
 * Dependencies : 
 * Brunet.Edge
 * Brunet.EdgeException
 * Brunet.EdgeListener;
 * Brunet.Packet;
 * Brunet.PacketParser;
 * Brunet.TransportAddress;
 * Brunet.UdpEdge;
 */

using Brunet;
using System;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Collections;

namespace Brunet
{

  /**
  * A EdgeListener that uses UDP for the underlying
  * protocol.  This listener creates UDP edges.
  * 
  * This uses UDP (and is compatible with nodes running other
  * UdpEdgeListener), but this uses the Asynchronous .NET interfaces.
  * It *may* perform much better, or it *may* cause deadlocks (due
  * to overuse of the ThreadPool).
  */

  public class ASUdpEdgeListener : EdgeListener, IPacketHandler
  {

    protected IPEndPoint ipep;
    protected Socket s;

    ///used for thread for the socket synchronization
    protected object _sync;
    protected object _read_lock;
    
    protected IAsyncResult _read_asr;
    ///Buffer to read the packets into
    protected byte[] _rec_buffer;
    protected byte[] _send_buffer;

    ///Here is the queue for outgoing packets:
    protected Queue _send_queue;

    /**
     * This is a simple little class just to hold the
     * two objects needed to do a send
     */
    private class SendQueueEntry {
      public SendQueueEntry(Packet p, UdpEdge udpe) {
        Packet = p;
        Sender = udpe;
      }
      public Packet Packet;
      public UdpEdge Sender;
    }

    /**
     * Hashtable of ID to Edges
     */
    protected Hashtable _id_ht;
    protected Hashtable _remote_id_ht;

    protected Random _rand;

    protected ArrayList _tas;
    public override ArrayList LocalTAs
    {
      get
      {
        return ArrayList.ReadOnly(_tas);
      }
    }

    public override TransportAddress.TAType TAType
    {
      get
      {
        return TransportAddress.TAType.Udp;
      }
    }

    protected bool _running;
    protected bool _isstarted;
    public override bool IsStarted
    {
      get { return _isstarted; }
    }

    //This is our best guess of the local endpoint
    protected IPEndPoint _local_ep;
    
    public ASUdpEdgeListener(int port):this(port, null)
    {
      
    }
    public ASUdpEdgeListener(int port, IPAddress[] ipList)
    {
      /**
       * We get all the IPAddresses for this computer
       */
      _tas = GetIPTAs(TransportAddress.TAType.Udp, port, ipList);
      
      IPAddress ipa = IPAddress.Loopback;
      bool stop = false;
      foreach(TransportAddress ta in _tas) {
        ArrayList ips = ta.GetIPAddresses();
	foreach(IPAddress ip in ips) {
          if( !IPAddress.IsLoopback(ip) && (ip.Address != 0) ) {
		  //0 is the 0.0.0.0, or any address
            ipa = ip;
	    stop = true;
	    break;
	  }
	}
	if( stop ) { break; }
      }
      //ipa, now holds our best guess for an endpoint..
      _local_ep = new IPEndPoint(ipa, port);
      /*
       * Use this to listen for data
       */
      ipep = new IPEndPoint(IPAddress.Any, port);
      s = new Socket(AddressFamily.InterNetwork,
                     SocketType.Dgram, ProtocolType.Udp);
      _id_ht = new Hashtable();
      _remote_id_ht = new Hashtable();
      _sync = new object();
      _read_lock = new object();
      _running = false;
      _isstarted = false;
      //There are two 4 byte IDs for each edge we need to make room for
      _rec_buffer = new byte[ 8 + Packet.MaxLength ];
      _send_buffer = new byte[ 8 + Packet.MaxLength ];
      _send_queue = new Queue();
      //Use our hashcode as the seed (terribly insecure business...)
      _rand = new Random( GetHashCode() );
    }

    /**
     * When a UdpEdge closes we need to remove it from
     * our table, so we will know it is new if it comes
     * back.
     */
    public void CloseHandler(object edge, EventArgs args)
    {
      UdpEdge e = (UdpEdge)edge;
      lock( _id_ht ) {
        _id_ht.Remove( e.ID );
	object re = _remote_id_ht[ e.RemoteID ];
	if( re == e ) {
          _remote_id_ht.Remove( e.RemoteID );
	}
      }
    }

    /**
     * Implements the EdgeListener function to 
     * create edges of this type.
     */
    public override void CreateEdgeTo(TransportAddress ta, EdgeCreationCallback ecb)
    {
      if( !IsStarted )
      {
        ecb(false, null,
            new EdgeException("UdpEdgeListener is not started") );
      }
      else if( ta.TransportAddressType != this.TAType ) {
        ecb(false, null,
            new EdgeException(ta.TransportAddressType.ToString()
                              + " is not my type: " + this.TAType.ToString() ) );
      }
      
      Edge e = null;
      ArrayList ip_addresses = ta.GetIPAddresses();
      IPAddress first_ip = (IPAddress)ip_addresses[0];

      IPEndPoint end = new IPEndPoint(first_ip, ta.Port);
      /* We have to keep our mapping of end point to edges up to date */
      lock( _id_ht ) {
        //Get a random ID for this edge:
        int id;
        do {
          id = _rand.Next();
        } while( _id_ht.Contains(id) || id == 0 );
        e = new UdpEdge(this, false, end, _local_ep, id, 0);
        _id_ht[id] = e;
      }
      /* Tell me when you close so I can clean up the table */
      e.CloseEvent += new EventHandler(this.CloseHandler);
      ecb(true, e, null);
    }
    
    /**
     * This method may be called once to start listening.
     * @throw Exception if start is called more than once (including
     * after a Stop
     */
    public override void Start()
    {
      lock( _sync ) {
        if( _isstarted ) {
          //We can't start twice... too bad, so sad:
          throw new Exception("Restart never allowed");
        }
        s.Bind(ipep);
        _isstarted = true;
      }
      lock( _read_lock ) {
        _running = true;
        EndPoint end = new IPEndPoint(IPAddress.Any, 0);
	//Console.WriteLine("About to BeingReceiveFrom");
        _read_asr = s.BeginReceiveFrom(_rec_buffer, 0, _rec_buffer.Length,
		         SocketFlags.None, ref end, this.ReceiveHandler, end);
      }
    }

    /**
     * To stop listening, this method is called
     */
    public override void Stop()
    {
      lock( _read_lock ) {
        _running = false;
        try {
          EndPoint end = (EndPoint)_read_asr;
          s.EndReceiveFrom(_read_asr, ref end);
	}
	catch(Exception x) {
          Console.Error.WriteLine("In ASUdpEdgeListener.Stop: {0}",x);
	}
	s.Close();
      }
    }

    /**
     * When we get a packet this event is called
     */
    protected void ReceiveHandler(IAsyncResult asr) {
      try {
      	
        EndPoint end = (EndPoint)asr.AsyncState;
        
	int rec_bytes = s.EndReceiveFrom(asr, ref end);
        //Get the id of this edge:
        int remoteid = NumberSerializer.ReadInt(_rec_buffer, 0);
        int localid = NumberSerializer.ReadInt(_rec_buffer, 4);
	bool read_packet = true;
        bool is_new_edge = false;
	UdpEdge edge = null;
        lock ( _id_ht ) {
          edge = (UdpEdge)_id_ht[localid];
          if( localid == 0 ) {
            //This is a potentially a new incoming edge
            is_new_edge = true;

            //Check to see if it is a dup:
            UdpEdge e_dup = (UdpEdge)_remote_id_ht[remoteid];
            if( e_dup != null ) {
              //Lets check to see if this is a true dup:
              if( e_dup.End.Equals( end ) ) {
                //Same id from the same endpoint, looks like a dup...
                is_new_edge = false;
                //Console.WriteLine("Stopped a Dup on: {0}", e_dup);
                //Reuse the existing edge:
                edge = e_dup;
              }
              else {
                //This is just a coincidence.
              }
            }
            if( is_new_edge ) {
              //We need to assign it a local ID:
              do {
                localid = _rand.Next();
              } while( _id_ht.Contains(localid) || localid == 0 );
              edge = new UdpEdge(this,
                               true, (IPEndPoint)end,
                               _local_ep, localid, remoteid);
              _id_ht[localid] = edge;
              _remote_id_ht[remoteid] = edge;
              edge.CloseEvent += new EventHandler(this.CloseHandler);
            }
          }
          else if ( edge == null ) {
            /*
             * This is the case where the Edge is not a new edge,
             * but we don't know about it.  It is probably an old edge
             * that we have closed.  We can ignore this packet
             */
            read_packet = false;
          }
          else if ( edge.RemoteID == 0 ) {
            /* This is the response to our edge creation */
            edge.RemoteID = remoteid;
          }
          else if( edge.RemoteID != remoteid ) {
            /*
             * This could happen as a result of packet loss or duplication
             * on the first packet.  We should ignore any packet that
             * does not have both ids matching.
             */
            read_packet = false;
            Console.WriteLine("Received Dup on: {0}", edge);
          }
        }
        //Drop the ht lock and announce the edge and the packet:
        if( is_new_edge ) {
          SendEdgeEvent(edge);
        }
        if( read_packet ) {
          Packet p = PacketParser.Parse(_rec_buffer, 8, rec_bytes - 8);
          //We have the edge, now tell the edge to announce the packet:
          edge.Push(p);
	  //Console.WriteLine("Got packet: {0}", p);
        }
	/*
	 * We have finished reading the packet, now read the next one
	 */
      }
      catch(Exception x) {
        System.Console.Error.WriteLine("Exception: {0}",x);
      }
      finally {
        lock( _read_lock ) {
          if( _running ) {
            //Start the next round:
            EndPoint end = new IPEndPoint(IPAddress.Any, 0);
            _read_asr = s.BeginReceiveFrom(_rec_buffer, 0,
			 _rec_buffer.Length, SocketFlags.None, ref end,
			 this.ReceiveHandler, end);
	  }
	}
      }
    }
    /**
     * When UdpEdge objects call Send, it calls this packet
     * callback:
     */
    public void HandlePacket(Packet p, Edge from)
    {
      //Console.WriteLine("About to StartSend on: {0}\n{1}",from, p); 
      lock( _send_queue ) {
        SendQueueEntry sqe = new SendQueueEntry(p, (UdpEdge)from);
        _send_queue.Enqueue(sqe);
	if( _send_queue.Count == 1 ) {
          //We have just one item, go ahead and start to send:
	  try {
	    StartSend(sqe);
	  }
	  catch(Exception x) {
            Console.Error.WriteLine("In HandlePacket.  Edge: {0}\n{1}", sqe.Sender, x);
	    /*
	     * This is a packet loss, remove it from the queue:
	     */
            _send_queue.Dequeue();
	  }
	}
	else {
          //There is already a send going on, it will run until
	  //it empties the queue.
	}
      }
    }
    
    /**
     * Make sure to hold the lock on the _send_queue *PRIOR* to
     * calling this method
     */
    private void StartSend(SendQueueEntry sqe) {
      Packet p = sqe.Packet;
      UdpEdge sender = sqe.Sender;
      EndPoint e = sender.End;
      //Write the IDs of the edge:
      //[local id 4 bytes][remote id 4 bytes][packet]
      NumberSerializer.WriteInt(sender.ID, _send_buffer, 0);
      NumberSerializer.WriteInt(sender.RemoteID, _send_buffer, 4);
      p.CopyTo(_send_buffer, 8);
      //Console.WriteLine("About to BeginSendTo"); 
      s.BeginSendTo(_send_buffer, 0, 8 + p.Length, SocketFlags.None, e,
			this.SendHandler, sqe);
    }
    
    protected void SendHandler(IAsyncResult asr) {
      try {
        //int sent = 
        s.EndSendTo(asr);
      }
      catch(Exception x) {
        /*
         * This is just a lost packet.  No big deal
         */
	SendQueueEntry sqeo = (SendQueueEntry)asr.AsyncState;
        Console.Error.WriteLine("In SendHandler, EndSendTo.  Edge: {0}\n{1}", sqeo.Sender, x);
      }
      //Console.WriteLine("EndSendTo"); 
      //Check to see if there is anymore to send:
      lock( _send_queue ) {
        //Remove the packet we just finished sending:
	_send_queue.Dequeue();
	//Now try to send another:
        bool done = ( _send_queue.Count == 0);
        while(!done) {
	  SendQueueEntry sqe = null;
          try {
            sqe = (SendQueueEntry)_send_queue.Peek();
            StartSend(sqe);
            done = true;
          }
          catch(Exception x) {
            Console.Error.WriteLine("StartSend failed: Edge: {0}\n{1}", sqe.Sender, x);
	    /*
	     * This is a packet loss, remove it from the queue:
	     */
            _send_queue.Dequeue();
	    done = ( _send_queue.Count == 0);
	    //If _send_queue.Count == 0, then there are no more to send, and we are done
          }
        }
      } //Release the lock
    }

  }
}
