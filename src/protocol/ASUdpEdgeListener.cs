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

  public class ASUdpEdgeListener : UdpEdgeListenerBase, IPacketHandler
  {

    protected IPEndPoint ipep;
    protected Socket s;

    ///used for thread for the socket synchronization
    protected object _read_lock;
    
    protected IAsyncResult _read_asr;

    public ASUdpEdgeListener(int port):this(port, null)
    {
      
    }
    public ASUdpEdgeListener(int port, IPAddress[] ipList)
           : this(port, ipList, null) { }
    public ASUdpEdgeListener(int port, IPAddress[] ipList, TAAuthorizer ta_auth)
    {
      /**
       * We get all the IPAddresses for this computer
       */
      _tas = GetIPTAs(TransportAddress.TAType.Udp, port, ipList);
      _local_ep = GuessLocalEndPoint(_tas);
      _ta_auth = ta_auth;
      if( _ta_auth == null ) {
        //Always authorize in this case:
        _ta_auth = new ConstantAuthorizer(TAAuthorizer.Decision.Allow);
      }
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
      _send_handler = this;
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
      else if( _ta_auth.Authorize(ta) == TAAuthorizer.Decision.Deny ) {
        //Too bad.  Can't make this edge:
        ecb(false, null,
            new EdgeException( ta.ToString() + " is not authorized") );
      }
      else { 
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
  	  if( id < 0 ) { id = ~id; }
          } while( _id_ht.Contains(id) || id == 0 );
          e = new UdpEdge(this, false, end, _local_ep, id, 0);
          _id_ht[id] = e;
        }
        /* Tell me when you close so I can clean up the table */
        e.CloseEvent += new EventHandler(this.CloseHandler);
        ecb(true, e, null);
      }
    }

    protected override void SendControlPacket(EndPoint end, int remoteid, int localid,
                                     ControlCode c)
    {
      lock(_sync) {
        byte[] tmp_buf = new byte[12];
        NumberSerializer.WriteInt(localid, tmp_buf, 0);
        //Bit flip to indicate this is a control packet
        NumberSerializer.WriteInt(~remoteid, tmp_buf, 4);
        NumberSerializer.WriteInt((int)c, tmp_buf, 8);

        try {	//catching SocketException
          System.Console.WriteLine("Sending control to: {0}", end);
          s.BeginSendTo(tmp_buf, 0, 12, SocketFlags.None, end,
			new AsyncCallback(this.SendControlPacketCallback), null);
        }
        catch (SocketException sc) {
          Console.Error.WriteLine(
            "Error in Socket.SendTo. Endpoint: {0}\n{1}", end, sc);
        }
      }
    }

    protected void SendControlPacketCallback(IAsyncResult asr)
    {
      try {
        s.EndSendTo(asr);
      }
      catch(Exception x) {
        Console.Error.WriteLine("{0}", x);
      }
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
		         SocketFlags.None, ref end, new AsyncCallback(this.ReceiveHandler), end);
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
        if( localid < 0 ) {
	    /*
	     * We never give out negative id's, so if we got one
	     * back the other node must be sending us a control
	     * message.
	     */
          HandleControlPacket(remoteid, localid, _rec_buffer);
	}
	else {
	  HandleDataPacket(remoteid, localid, _rec_buffer, 8,
                             rec_bytes - 8, end);
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
			 new AsyncCallback(this.ReceiveHandler), end);
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
			new AsyncCallback(this.SendHandler), sqe);
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
