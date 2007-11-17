/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Brunet
{
  /**
   * We use Brunet BroadcastRPC due to there not being a single zeroconf 
   * service for all OS's, this makes use of multicast, specifically 
   * destination 224.123.123.222:56018.  Use a random UDP port for unicast 
   * communication.
   *
   * Once  you've registered using "Register" a BroadcastRPCHandler method any 
   * incoming request will be sent to that handler.  Responses to incoming
   * requests should be sent via "SendResponse".  When looking for services
   * use "Announce" and it will send via the multicast socket to all listeners.
   */

  public class BroadcastRPC
  {
    protected static Socket _mc, _uc;
    public Socket UnicastSocket { get { return _uc; } }
    public static readonly IPAddress mc_addr = IPAddress.Parse("224.123.123.222");
    public static readonly int mc_port = 56123;
    public static readonly EndPoint mc_endpoint = new IPEndPoint(mc_addr, mc_port);
    protected bool _running;
    protected Node _node;

    protected class StateObject
    {
      public EndPoint ep;
      public byte[] buffer;
      public Socket socket;

      public StateObject(Socket sock) {
        ep = new IPEndPoint(IPAddress.Any, 0);
        buffer = new byte[Packet.MaxLength];
        socket = sock;
      }

      public void Update() {
        ep = new IPEndPoint(IPAddress.Any, 0);
      }
    }

    public BroadcastRPC(Node node) {
      _node = node;
      try {
        _mc = new Socket(AddressFamily.InterNetwork, SocketType.Dgram,
                        ProtocolType.Udp);
        // Allows for multiple Multicast clients on the same host!
        _mc.SetSocketOption(SocketOptionLevel.Socket, 
                                SocketOptionName.ReuseAddress, true);
        _mc.Bind(new IPEndPoint(IPAddress.Any, mc_port));
        _mc.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
          new MulticastOption(mc_addr, IPAddress.Any));
      }
      catch {
        _mc = null;
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, "Unable to start listening on the" +
            "multicast port, but we can still send and request services.");
      }

      _uc = new Socket(AddressFamily.InterNetwork, SocketType.Dgram,
                       ProtocolType.Udp);
      _uc.Bind(new IPEndPoint(IPAddress.Any, 0));

      _running = true;
      if(_mc != null) {
        BeginReceive(new StateObject(_mc));
      }
      BeginReceive(new StateObject(_uc));
    }

    public void Stop() {
      _running = false;
      _uc.Close();
      if(_mc != null) {
        _mc.Close();
      }
    }

    protected void BeginReceive(StateObject so) {
      try {
        so.socket.BeginReceiveFrom(so.buffer, 0, so.buffer.Length, 0, 
                                   ref so.ep, ReceiveHandler, so);
      }
      catch(ObjectDisposedException x) {
        if(_running) {
          ProtocolLog.WriteIf(ProtocolLog.Exceptions, "ERROR: " + x.ToString());
        }
      }
      catch(Exception x) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, x.ToString());
      }
    }

    /**
     * This is both where the Unicast and Multicast socket wait for their
     * receives.  So far there are only two types of incoming messages
     * Notification and Request.  A Notification contains TAs, while a request
     * is a request for those TAs.
     */
    protected void ReceiveHandler(IAsyncResult asr)
    {
      StateObject so = (StateObject) asr.AsyncState;
      int rec_bytes = 0;
      try {
        rec_bytes = _mc.EndReceiveFrom(asr, ref so.ep);
        MemBlock packet = MemBlock.Reference(so.buffer, 0, rec_bytes);
        UnicastSender us = new UnicastSender(_node, (IPEndPoint) so.ep);
        _node.Announce(packet, us);
      }
      catch(ObjectDisposedException odx) {
        //If we are no longer running, this is to be expected.
        if(_running) {
        //If we are running print it out
          ProtocolLog.WriteIf(ProtocolLog.Exceptions, odx.ToString());
        }
      }
      catch(Exception x) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, x.ToString());
      }
      finally {
        if(_running) {
          //Start the next round:
          so.Update();
          BeginReceive(so);
        }
      }
    }

    public void Send(IPEndPoint ep, ICopyable data)
    {
      // Silly users can trigger a handful of exceptions here...
      try {
        byte[] buffer = new byte[data.Length];
        int length = data.CopyTo(buffer, 0);

        _uc.BeginSendTo(buffer, 0, length, 0, ep,
                       EndSendHandler, null);
      }
      catch (Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, "ERROR: " + e);
      }
    }

    protected virtual void EndSendHandler(IAsyncResult asr)
    {
      // Shouldn't get any exceptions here, but its better to not leave 
      // them unhandled.
      try {
        _uc.EndSendTo(asr);
      }
      catch (Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, "ERROR: " + e);
      }
    }
  }

  public abstract class IPSender: ISender
  {
    protected IPEndPoint _ipep;
    protected Node _node;

    public virtual void Send(ICopyable data)
    {
      _node.BroadcastRPC.Send(_ipep, data);
    }
  }

  public class UnicastSender: IPSender
  {
    public UnicastSender(Node node, IPEndPoint ipep)
    {
      _node = node;
      _ipep = ipep;
    }
  }

  public class MulticastSender: IPSender
  {
    public MulticastSender(Node node)
    {
      _node = node;
      _ipep = (IPEndPoint) BroadcastRPC.mc_endpoint;
    }
  }
}
