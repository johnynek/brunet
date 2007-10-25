/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2005-2007  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System.IO;
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

  public class ASUdpEdgeListener : UdpEdgeListenerBase, IEdgeSendHandler
  {

    protected IPEndPoint ipep;
    protected Socket s;

    ///used for thread for the socket synchronization
    protected object _read_lock;
    
    protected IAsyncResult _read_asr;

    protected byte[] _send_buffer;

    protected bool _sending;

    public ASUdpEdgeListener(int port) : this(port, null, null)
    {
    }
    public ASUdpEdgeListener(int port, IEnumerable ipList) : this(port, ipList, null)
    {
    }

    /**
     * @param port the local port to bind to
     * @param local_config_ips an IEnumerable object which gives the list of local
     * ips.  This is consulted every time LocalTAs is accessed, so it can
     * change as new interfaces are added
     * @param ta_auth the TAAuthorizer for packets incoming
     */
    public ASUdpEdgeListener(int port, IEnumerable local_config_ips, TAAuthorizer ta_auth)
    {
      /**
       * We get all the IPAddresses for this computer
       */
      if( local_config_ips == null ) {
        _tas = TransportAddressFactory.CreateForLocalHost(TransportAddress.TAType.Udp, port);
      }
      else {
        _tas = TransportAddressFactory.Create(TransportAddress.TAType.Udp, port, local_config_ips);
      }

      _nat_hist = null;
      _nat_tas = new NatTAs( _tas, _nat_hist );
      _ta_auth = ta_auth;
      if( _ta_auth == null ) {
        //Always authorize in this case:
        _ta_auth = new ConstantAuthorizer(TAAuthorizer.Decision.Allow);
      }
      /*
       * Use this to listen for data
       */
      _port = port;
      ipep = new IPEndPoint(IPAddress.Any, port);
      s = new Socket(AddressFamily.InterNetwork,
                     SocketType.Dgram, ProtocolType.Udp);
      _id_ht = new Hashtable();
      _remote_id_ht = new Hashtable();
      _sync = new object();
      _read_lock = new object();
      _running = false;
      _isstarted = false;
      
      _send_buffer = new byte[ 8 + Packet.MaxLength ];
      _send_queue = new Queue();
      ///@todo we need to use the cryptographic RNG
      _rand = new Random();
      _send_handler = this;
      _sending = false;
    }

    protected override void SendControlPacket(EndPoint end, int remoteid, int localid,
                                     ControlCode c, object state)
    {
      lock(_sync) {
        using( MemoryStream ms = new MemoryStream() ) {
          NumberSerializer.WriteInt(localid, ms);
          //Bit flip to indicate this is a control packet
          NumberSerializer.WriteInt(~remoteid, ms);
          NumberSerializer.WriteInt((int)c, ms);
          if( c == ControlCode.EdgeDataAnnounce ) {
            UdpEdge e = (UdpEdge)_id_ht[localid];
            if( (e != null) && (e.RemoteID == remoteid) ) {
              Hashtable t = new Hashtable();
              t["RemoteTA"] = e.RemoteTA.ToString();
              t["LocalTA"] = e.LocalTA.ToString();
              AdrConverter.Serialize(t, ms);
            }
            else {
              ProtocolLog.WriteIf(ProtocolLog.UdpEdge, String.Format(
                "Problem sending EdgeData: EndPoint: {0}, remoteid: {1}, localid: {2}, Edge: {3}",
                end, remoteid, localid, e));
            }
          }
          try {        //catching SocketException
            byte[] tmp_buf = ms.ToArray();
            ProtocolLog.WriteIf(ProtocolLog.UdpEdge, String.Format("Sending control to: {0}", end));
            s.BeginSendTo(tmp_buf, 0, tmp_buf.Length, SocketFlags.None, end,
                          new AsyncCallback(this.SendControlPacketCallback), null);
          }
          catch (SocketException sc) {
            ProtocolLog.WriteIf(ProtocolLog.UdpEdge, String.Format(
              "Error in Socket.SendTo. Endpoint: {0}\n{1}", end, sc));
          }
        }
      }
    }

    protected void SendControlPacketCallback(IAsyncResult asr)
    {
      try {
        s.EndSendTo(asr);
      }
      catch(System.ObjectDisposedException odx) {
        //If we are no longer running, this is to be expected.
        if( _running ) {
          //If we are running print it out
          ProtocolLog.WriteIf(ProtocolLog.UdpEdge, String.Format("{0}", odx));
        }
      }
      catch(Exception x) {
        ProtocolLog.WriteIf(ProtocolLog.UdpEdge, String.Format("{0}", x));
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
      _running = true;
      //Console.Error.WriteLine("About to BeingReceiveFrom");
      object[] state = new object[2];
      
      EndPoint end = new IPEndPoint(IPAddress.Any, 0);
      state[0] = end;
      BufferAllocator ba = new BufferAllocator(8 + Packet.MaxLength);
      state[1] = ba;
        
      int max = ba.Buffer.Length - ba.Offset;
      _read_asr = s.BeginReceiveFrom(ba.Buffer, ba.Offset, max,
                         SocketFlags.None, ref end, new AsyncCallback(this.ReceiveHandler), state);
    }

    /**
     * To stop listening, this method is called
     */
    public override void Stop()
    {
      lock( _read_lock ) {
        _running = false;
        try {
          s.Close();
          //EndPoint end = (EndPoint)_read_asr.AsyncState;
          //s.EndReceiveFrom(_read_asr, ref end);
        }
        catch(Exception x) {
          ProtocolLog.WriteIf(ProtocolLog.UdpEdge, String.Format("In ASUdpEdgeListener.Stop: {0}",x));
        }
      }
    }

    /**
     * When we get a packet this event is called
     */
    protected void ReceiveHandler(IAsyncResult asr) {
      object[] state = (object[])asr.AsyncState;
      EndPoint end = (IPEndPoint)state[0];
      BufferAllocator ba = (BufferAllocator)state[1];

      try {
        int rec_bytes = s.EndReceiveFrom(asr, ref end);
        //Get the id of this edge:
        int remoteid = NumberSerializer.ReadInt(ba.Buffer, ba.Offset);
        int localid = NumberSerializer.ReadInt(ba.Buffer, ba.Offset + 4);
        MemBlock packet = MemBlock.Reference(ba.Buffer, ba.Offset + 8, rec_bytes - 8);
        ba.AdvanceBuffer( rec_bytes );
        if( localid < 0 ) {
            /*
             * We never give out negative id's, so if we got one
             * back the other node must be sending us a control
             * message.
             */
          HandleControlPacket(remoteid, localid, packet, null);
        }
        else {
          HandleDataPacket(remoteid, localid, packet, end, null);
        }
        /*
         * We have finished reading the packet, now read the next one
         */
      }
      catch(System.ObjectDisposedException odx) {
        //If we are no longer running, this is to be expected.
        if( _running ) {
          //If we are running print it out
          ProtocolLog.WriteIf(ProtocolLog.UdpEdge, String.Format("{0}", odx));
        }
      }
      catch(Exception x) {
        ProtocolLog.WriteIf(ProtocolLog.UdpEdge, String.Format("Exception: {0}",x));
      }
      finally {
        if( _running ) {
          //Start the next round:
          end = new IPEndPoint(IPAddress.Any, 0);
          state[0] = end;
          int max = ba.Buffer.Length - ba.Offset;
          _read_asr = s.BeginReceiveFrom(ba.Buffer, ba.Offset,
                         max, SocketFlags.None, ref end,
                         new AsyncCallback(this.ReceiveHandler), state);
        }
      }
    }
    /**
     * When UdpEdge objects call Send, it calls this packet
     * callback:
     */
    public override void HandleEdgeSend(Edge from, ICopyable p)
    {
      //Console.Error.WriteLine("About to StartSend on: {0}\n{1}",from, p); 
      bool startsend = false;
      SendQueueEntry sqe = new SendQueueEntry(p, (UdpEdge)from);
      lock( _send_queue ) {
        _send_queue.Enqueue(sqe);
        startsend = !_sending;
        if(startsend)
          _sending = true;
      }
      if(startsend) {
        //We have just one item, go ahead and start to send:
        try {
          StartSend(sqe);
        }
        catch(Exception x) {
          ProtocolLog.WriteIf(ProtocolLog.UdpEdge, String.Format(
            "In HandlePacket.  Edge: {0}\n{1}", sqe.Sender, x));
          /*
           * This is a packet loss, remove it from the queue:
           */
          _send_queue.Dequeue();
          _sending = false;
        }
      }
      else {
        //There is already a send going on, it will run until
        //it empties the queue.
      }
    }

    /**
     * Make sure to hold the lock on the _send_queue *PRIOR* to
     * calling this method
     */
    private void StartSend(SendQueueEntry sqe) {
      if( !_running ) {
        //Don't even bother if we are not running
        lock( _send_queue ) {
          //Make sure the queue is empty
          _send_queue.Clear();
        }
        return;
      }
      ICopyable p = sqe.Packet;
      UdpEdge sender = sqe.Sender;
      EndPoint e = sender.End;
      //Write the IDs of the edge:
      //[local id 4 bytes][remote id 4 bytes][packet]
      NumberSerializer.WriteInt(sender.ID, _send_buffer, 0);
      NumberSerializer.WriteInt(sender.RemoteID, _send_buffer, 4);
      p.CopyTo(_send_buffer, 8);
      //Console.Error.WriteLine("About to BeginSendTo"); 
      s.BeginSendTo(_send_buffer, 0, 8 + p.Length, SocketFlags.None, e,
                        new AsyncCallback(this.SendHandler), sqe);
    }
    
    protected void SendHandler(IAsyncResult asr) {
      try {
        //int sent = 
        s.EndSendTo(asr);
      }
      catch(System.ObjectDisposedException odx) {
        //If we are no longer running, this is to be expected.
        if( _running ) {
          //If we are running print it out
          ProtocolLog.WriteIf(ProtocolLog.UdpEdge, String.Format("{0}", odx));
        }
      }
      catch(Exception x) {
        /*
         * This is just a lost packet.  No big deal
         */
        SendQueueEntry sqeo = (SendQueueEntry)asr.AsyncState;
        ProtocolLog.WriteIf(ProtocolLog.UdpEdge, String.Format(
          "In SendHandler, EndSendTo.  Edge: {0}\n{1}", sqeo.Sender, x));
      }
      //Console.Error.WriteLine("EndSendTo"); 
      //Check to see if there is anymore to send:
      if( !_running ) {
        //Don't even bother if we are not running
        //Make sure the queue is empty
        lock(_send_queue) {
          _send_queue.Clear();
          _sending = false;
        }
        return;
      }
      bool done = true;
      lock(_send_queue) {
        //Remove the packet we just finished sending:
        _send_queue.Dequeue();
        //Now try to send another:
        done = ( _send_queue.Count == 0);
        _sending = !done;
      }

      while(!done) {
        SendQueueEntry sqe = null;
        try {
          sqe = (SendQueueEntry)_send_queue.Peek();
          StartSend(sqe);
          done = true;
        }
        catch(Exception x) {
          ProtocolLog.WriteIf(ProtocolLog.UdpEdge, String.Format(
            "StartSend failed: Edge: {0}\n{1}", sqe.Sender, x));
          /*
           * This is a packet loss, remove it from the queue:
           */
          lock(_send_queue) {
            done = ( _send_queue.Count == 0);
            if(!done)
              _send_queue.Dequeue();
            done = ( _send_queue.Count == 0);
            _sending = !done;
          }
          //If _send_queue.Count == 0, then there are no more to send, and we are done
        }
      }
    }
  }
}
