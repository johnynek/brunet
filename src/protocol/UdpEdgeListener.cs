/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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

  public class UdpEdgeListener:EdgeListener
  {

    protected IPEndPoint ipep;
    protected Socket s;

    ///used for thread for the socket synchronization
    protected object _sync;
    ///this is the thread were the socket is read:
    protected Thread _thread;
    ///Buffer to read the packets into
    protected byte[] _packet_buffer;

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
    public override bool IsStarted
    {
      get { return _running; }
    }

    public UdpEdgeListener(int port)
    {
      /**
       * We get all the IPAddresses for this computer
       */
      String StrLocalHost =  (Dns.GetHostName());
      IPHostEntry IPEntry = Dns.GetHostByName (StrLocalHost);
      IPAddress [] addr = IPEntry.AddressList;
      _tas = new ArrayList();
      foreach(IPAddress a in IPEntry.AddressList) {
        /**
         * We add Loopback addresses to the back, all others to the front
         * This makes sure non-loopback addresses are listed first.
         */
        if( IPAddress.IsLoopback(a) ) {
          //Put it at the back
          _tas.Add( new TransportAddress(TransportAddress.TAType.Udp,
                                         new IPEndPoint(a, port) ) );
        }
        else {
          //Put it at the front
          _tas.Insert(0, new TransportAddress(TransportAddress.TAType.Udp,
                                              new IPEndPoint(a, port) ) );
        }
      }

      /*
       * Use this to listen for data
       */
      ipep = new IPEndPoint(IPAddress.Any, port);
      s = new Socket(AddressFamily.InterNetwork,
                     SocketType.Dgram, ProtocolType.Udp);
      _id_ht = new Hashtable();
      _sync = new object();
      _running = false;
      //There is a 4 byte ID for each edge we need to make room for
      _packet_buffer = new byte[ 4 + Packet.MaxLength ];
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
        } while( _id_ht.Contains(id) );

        e = new UdpEdge(new Edge.PacketCallback(this.SendCallback),
                        false, end, (IPEndPoint)s.LocalEndPoint, id);
        _id_ht[id] = e;
      }
      /* Tell me when you close so I can clean up the table */
      e.CloseEvent += new EventHandler(this.CloseHandler);
      ecb(true, e, null);
    }

    public override void Start()
    {
      lock( _sync ) {
        s.Bind(ipep);
      }
      _running = true;
      _thread = new Thread( new ThreadStart(this.SocketThread) );
      _thread.Start();
    }
    public override void Stop()
    {
      _running = false;
    }

    /**
     * This is a System.Threading.ThreadStart delegate
     * We loop waiting for edges that need to send,
     * or data on the socket.
     */
    protected void SocketThread()
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
        if( read ) {
          //Get the id of this edge:
          int id = NumberSerializer.ReadInt(_packet_buffer, 0);
          lock ( _id_ht ) {
            if (! _id_ht.Contains(id)) {
              edge = new UdpEdge(new Edge.PacketCallback(this.SendCallback),
                                 true, (IPEndPoint)end,
                                 (IPEndPoint)s.LocalEndPoint, id);
              /* Tell me when you close so I can clean up the table */
              edge.CloseEvent += new EventHandler(this.CloseHandler);
              _id_ht[id] = edge;
              is_new_edge = true;
            }
            else {
              edge = (UdpEdge) _id_ht[id];
            }
          }
          //Drop the ht lock and announce the edge and the packet:
          if( is_new_edge ) {
            SendEdgeEvent(edge);
          }
          Packet p = PacketParser.Parse(_packet_buffer, 4, rec_bytes - 4);
          //We have the edge, now tell the edge to announce the packet:
          edge.Push(p);
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
              more_to_send = _send_queue.Count > 0;
            }
          } //Release the lock
          if( sqe != null ) {
            //We have a packet to send
            Packet p = sqe.Packet;
            UdpEdge sender = sqe.Sender;
            EndPoint e = sender.End;
            //Get the lock on the socket (and buffer) to send
            lock( _sync ) {
              //Write the ID of the edge:
              NumberSerializer.WriteInt(sender.ID, _packet_buffer, 0);
              p.CopyTo(_packet_buffer, 4);
              s.SendTo(_packet_buffer, 0, 4 + p.Length, SocketFlags.None, e);
            }
          }
        } while( more_to_send );
        //Now it is time to see if we can read...
      }
    }

    /**
     * When UdpEdge objects call Send, it calls this packet
     * callback:
     */
    protected void SendCallback(Packet p, Edge from)
    {
      lock( _send_queue ) {
        SendQueueEntry sqe = new SendQueueEntry(p, (UdpEdge)from);
        _send_queue.Enqueue(sqe);
      }
    }

  }
}
