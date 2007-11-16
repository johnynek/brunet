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
   * BroadcastRPCHandler's receive are called by the ReceiveHandler.  They are
   * given the remote calling end point and an IList containing the packet's
   * data.
   */
  public delegate void BroadcastRPCHandler(EndPoint remote_ep, string method, IList data);

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
    protected Socket _mc, _uc;
    protected EndPoint _mc_endpoint;
    protected static readonly IPAddress _mc_addr = IPAddress.Parse("224.123.123.222");
    protected static readonly int _mc_port = 56123;
    protected Dictionary<string, BroadcastRPCHandler> _handlers;
    protected bool _running;

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

    public BroadcastRPC() {
      _mc = new Socket(AddressFamily.InterNetwork, SocketType.Dgram,
                       ProtocolType.Udp);
      // Allows for multiple Multicast clients on the same host!
      _mc.SetSocketOption(SocketOptionLevel.Socket, 
                              SocketOptionName.ReuseAddress, true);
      _mc_endpoint = new IPEndPoint(_mc_addr, _mc_port);
      _mc.Bind(new IPEndPoint(IPAddress.Any, _mc_port));
      _mc.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
        new MulticastOption(_mc_addr, IPAddress.Any));

      _uc = new Socket(AddressFamily.InterNetwork, SocketType.Dgram,
                       ProtocolType.Udp);
      _uc.Bind(new IPEndPoint(IPAddress.Any, 0));

      _handlers = new Dictionary<string, BroadcastRPCHandler>();

      _running = true;
      BeginReceive(new StateObject(_mc));
      BeginReceive(new StateObject(_uc));
    }

    public void Stop() {
      _running = false;
      _uc.Close();
      _mc.Close();
    }

    /**
     * Registers a BroadcastRPCHandler to a method name, similar to rpc.
     * @param name the string to associate the BroadcastRPCHandler to
     * @param method the method to call in the Receive loop if name is found
     */
    public void Register(string name, BroadcastRPCHandler method)
    {
      lock(_handlers) {
        if(_handlers.ContainsKey(name) && _handlers[name] != method) {
          throw new Exception("Attempted to register a new method with a pre-existing method name");
        }
        _handlers[name] = method;
      }
    }

    /**
     * Removes a BroadcastRPCHandler
     * @param name the string mapping to the method to remove from the 
     * BroadcastRPCHandlers
     */
    public void UnRegister(string name)
    {
      lock(_handlers) {
      _handlers.Remove(name);
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
        IList adr_data = (IList) AdrConverter.Deserialize(packet);
        string type = (string) adr_data[0];
        BroadcastRPCHandler callback = _handlers[type];
        if(callback == null) {
          throw new Exception("Invalid request");
        }
        if(ProtocolLog.LocalCO.Enabled) {
          ProtocolLog.WriteIf(ProtocolLog.LocalCO, "Type = " + type);
        }
        callback(so.ep, type, adr_data[1] as IList);
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

    /**
     * Used to Send multicast packets makes use of SendResponse.
     * @param method the string registered to the method you would like to call
     * @param data an IList containing the data to ship, this can be an 
     * ArrayList or an object array.
     */
    public void Announce(string method, IList data)
    {
      SendResponse(_mc_endpoint, method, data);
    }

    /**
     * Creates a packet for the remote end point and ships it off.
     * @param ep the EndPoint to send the data to, most likely and IPEndPoint
     * @param method the string registered to the method you would like to call
     * @param data an IList containing the data to ship, this can be an 
     * ArrayList or an object array.
     */
    public void SendResponse(EndPoint ep, string method, IList data)
    {
      object[] response = new object[2];
      response[0] = method;
      response[1] = data;
      using(MemoryStream ms = new MemoryStream()) {
        int length = 0;
        length = AdrConverter.Serialize(response, ms);
        byte[] buffer = ms.GetBuffer();
        // We can possibly get an exception here, oh well, just don't let it 
        // affect the underlying thread
        try {
          _uc.BeginSendTo(buffer, 0, length, 0, ep, 
                        EndSendHandler, null);
        }
        catch(Exception e) {
          ProtocolLog.WriteIf(ProtocolLog.Exceptions, "ERROR: " + e);
        }
      }
    }

    /**
     * Just a generic way to end all Aysnchronous Sends, we should only be
     * making async sends with the unicast socket, so need to make this any
     * fancier.
     */

    protected void EndSendHandler(IAsyncResult asr)
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
}
