/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2005  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
  * The UdpEdgeListener creates one thread.  In that
  * thread it loops processing reads.  The UdpEdgeListener
  * keeps a Queue of packets to send also.  After each
  * read attempt, it sends all the packets in the Queue.
  *
  */

  public class UdpEdgeListener : EdgeListener, IPacketHandler
  {

    protected IPEndPoint ipep;
    protected Socket s;

    ///used for thread for the socket synchronization
    protected object _sync;
    ///this is the thread were the socket is read:
    protected Thread _thread;
    ///Buffer to read the packets into
    protected byte[] _packet_buffer;
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
    
    public UdpEdgeListener(int port):this(port, null)
    {
      
    }
    public UdpEdgeListener(int port, IPAddress[] ipList)
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
      _sync = new object();
      _running = false;
      _isstarted = false;
      //There are two 4 byte IDs for each edge we need to make room for
      _packet_buffer = new byte[ 8 + Packet.MaxLength ];
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
        _running = true;
      }
      _thread = new Thread( new ThreadStart(this.SocketThread) );
      _thread.Start();
    }

    /**
     * To stop listening, this method is called
     */
    public override void Stop()
    {
      _running = false;
    }

    /**
     * This is a System.Threading.ThreadStart delegate
     * We loop waiting for edges that need to send,
     * or data on the socket.
     */
    protected void SocketThread() // error happening here
    {
      //Wait 10 ms before giving up on a read
      int microsecond_timeout = 10000;
      while(_running) {
        bool read = false;
        bool is_new_edge = false;
        UdpEdge edge = null;
        EndPoint end = new IPEndPoint(IPAddress.Any, 0);

        /**
         * Note that at no time do we hold two locks, or
         * do we hold a lock across an external function call or event
         */
        //Read if we can:
        int rec_bytes = 0;
        lock( _sync ) {
          read = s.Poll( microsecond_timeout, SelectMode.SelectRead );
          if( read ) {
            rec_bytes = s.ReceiveFrom(_packet_buffer, ref end);
          }
        }
        //Drop the socket lock.  We either read or we didn't
        bool read_packet = true;
        if( read ) {
          //Get the id of this edge:
          int remoteid = NumberSerializer.ReadInt(_packet_buffer, 0);
          int localid = NumberSerializer.ReadInt(_packet_buffer, 4);
          lock ( _id_ht ) {
            edge = (UdpEdge)_id_ht[localid];
            if( localid == 0 ) {
              //This is a new incoming edge
              is_new_edge = true;
              //We need to assign it a local ID:
              do {
                localid = _rand.Next();
              } while( _id_ht.Contains(localid) || localid == 0 );
              edge = new UdpEdge(this,
                                 true, (IPEndPoint)end,
                                 _local_ep, localid, remoteid);
              _id_ht[localid] = edge;
              edge.CloseEvent += new EventHandler(this.CloseHandler);
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
            }
          }
          //Drop the ht lock and announce the edge and the packet:
          if( is_new_edge ) {
            SendEdgeEvent(edge);
          }
          if( read_packet ) {
            Packet p = PacketParser.Parse(_packet_buffer, 8, rec_bytes - 8);
            //We have the edge, now tell the edge to announce the packet:
            edge.Push(p);
          }
        }
        /*
         * We are done with handling the reads.  Now lets
         * deal with all the pending sends:
         */
        SendQueueEntry sqe = null;
        bool more_to_send = false;
        do {
          sqe = null;
          lock( _send_queue ) {
            if( _send_queue.Count > 0 ) {
              sqe = (SendQueueEntry)_send_queue.Dequeue();
      //        if (sqe != null) { Send(sqe); }
              more_to_send = _send_queue.Count > 0;
            }
          } //Release the lock
          lock( _sync ) { if (sqe != null) { Send(sqe); } }
        } while( more_to_send );
        //Now it is time to see if we can read...
      }
      lock( _sync ) {
        s.Close();
      }
    }

    private void Send(SendQueueEntry sqe)
    {
      //We have a packet to send
      Packet p = sqe.Packet;
      UdpEdge sender = sqe.Sender;
      EndPoint e = sender.End;
      //Write the IDs of the edge:
      //[local id 4 bytes][remote id 4 bytes][packet]
      NumberSerializer.WriteInt(sender.ID, _send_buffer, 0);
      NumberSerializer.WriteInt(sender.RemoteID, _send_buffer, 4);
      p.CopyTo(_send_buffer, 8);
	      
      try {	//catching SocketException
        s.SendTo(_send_buffer, 0, 8 + p.Length, SocketFlags.None, e);
      }
      catch (SocketException sc) {
        Console.Error.WriteLine("Error in Socket send: {0}", sc);
      }
    }

    /**
     * When UdpEdge objects call Send, it calls this packet
     * callback:
     */
    public void HandlePacket(Packet p, Edge from)
    {
      lock( _send_queue ) {
        SendQueueEntry sqe = new SendQueueEntry(p, (UdpEdge)from);
        _send_queue.Enqueue(sqe);
        
        ///@todo this could be very unsafe.  We are assuming it is okay to
        ///send while receiving (which seems to work), but it may not be okay
        //Send(sqe);
      }
    }

  }
}
