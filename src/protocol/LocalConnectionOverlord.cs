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

/**
 * Dependencies
 * Brunet.ConnectionType
 * Brunet.BrunetLogger
 */

using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Brunet
{

  /**
   * This CO is used for Brunet Zeroconf, due to there not being a single
   * zeroconf service for all OS's, this makes use of multicast, specifically
   * destination 224.123.123.222:56018.  Use a random UDP port for unicast
   * communication.
   */

  public class LocalConnectionOverlord: ConnectionOverlord
  {

    // Let's get a cap on all messages in the LCO
    public static readonly int MaxPacketLength = 1200;
    private Node _node;
    private Socket _mc, _uc;
    private EndPoint _mc_endpoint;
    private static readonly IPAddress _mc_addr = IPAddress.Parse("224.123.123.222");
    private static readonly int _mc_port = 56123;
    private enum _ptype {Request, Notification};
    private DateTime _last_call;

    bool _active;
    /**
     * When IsActive is false, the ConnectionOverlord does nothing
     * to replace lost connections, or to get connections it needs.
     * When IsActive is true, then any missing connections are
     * made up for
     */
    public override bool IsActive
    {
      get { return _active; }
      set { _active = value; }
    }

    protected class StateObject
    {
      public EndPoint ep;
      public byte[] buffer;
      public Socket socket;

      public StateObject(Socket sock) {
        ep = new IPEndPoint(IPAddress.Any, 0);
        buffer = new byte[MaxPacketLength];
        socket = sock;
      }

      public void Update() {
        ep = new IPEndPoint(IPAddress.Any, 0);
      }
    }

    public LocalConnectionOverlord(Node node) {
      _node = node;
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
    }

    /**
     * If IsActive, then start trying to get connections.
     */
    public override void Activate() {
      BeginReceive(new StateObject(_mc));
      BeginReceive(new StateObject(_uc));
      _last_call = DateTime.UtcNow;
      Announce();
      _node.HeartBeatEvent += CheckConnection;
    }

    /**
     * @return true if the ConnectionOverlord needs a connection
     */
    public override bool NeedConnection
    {
      get { return _node.ConnectionTable.TotalCount == 0; }
    }
    /**
     * @return true if the ConnectionOverlord has sufficient connections
     *  for connectivity (no routing performance yet!)
     */
    public override bool IsConnected
    {
      get { throw new Exception("Not implemented!  LocalConnectionOverlord.IsConnected"); }
    }

    public void CheckConnection(object o, EventArgs ea)
    {
      if(!NeedConnection) {
        return;
      }
      DateTime now = DateTime.UtcNow;
      if(now - _last_call > TimeSpan.FromSeconds(60)) {
        _last_call = now;
        Announce();
      }
    }

    protected void Announce()
    {
//      Console.WriteLine("Announce!");
      SendRequest();
      SendNotification(_mc_endpoint);
    }

    protected void BeginReceive(StateObject so) {
      so.socket.BeginReceiveFrom(so.buffer, 0, MaxPacketLength, 0, ref so.ep,
                               ReceiveHandler, so);
    }

    protected void ReceiveHandler(IAsyncResult asr)
    {
      StateObject so = (StateObject) asr.AsyncState;
      int rec_bytes = 0;
      try {
        rec_bytes = _mc.EndReceiveFrom(asr, ref so.ep);
        MemBlock packet = MemBlock.Reference(so.buffer, 0, rec_bytes);
        IList adr_data = (IList) AdrConverter.Deserialize(packet);
        _ptype type = (_ptype) adr_data[0];
        switch(type) {
          case _ptype.Request:
            SendNotification(so.ep);
            break;
          case _ptype.Notification:
            HandleNotification((IList) adr_data[1]);
            break;
          default:
            Console.WriteLine("poop");
            ProtocolLog.WriteIf(ProtocolLog.LocalCO, "Invalid ptype " + (int) type);
            break;
        }
      }
      catch(System.ObjectDisposedException odx) {
        //If we are no longer running, this is to be expected.
        if(_active) {
        //If we are running print it out
          Console.WriteLine(odx);
          ProtocolLog.WriteIf(ProtocolLog.Exceptions, odx.ToString());
        }
      }
      catch(Exception x) {
        Console.WriteLine(x);
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, x.ToString());
      }
      finally {
        if(_active) {
          //Start the next round:
          so.Update();
          BeginReceive(so);
        }
      }
    }

    protected void HandleNotification(IList tas)
    {
      ArrayList remote_tas = new ArrayList(tas.Count);
      // Test to make sure they really are TAs
      foreach(string ta in tas) {
        try {
          remote_tas.Add(new IPTransportAddress(ta));
        }
        catch(Exception e) {
          ProtocolLog.WriteIf(ProtocolLog.Exceptions, "Invalid IPTA: " + e);
        }
      }
      _node.UpdateRemoteTAs(remote_tas);
    }

    protected void SendRequest()
    {
      object[] response = new object[1];
      response[0] = (int) _ptype.Request;
      using(MemoryStream ms = new MemoryStream()) {
        AdrConverter.Serialize(response, ms);
        byte[] buffer = ms.GetBuffer();
        _uc.BeginSendTo(buffer, 0, buffer.Length, 0, _mc_endpoint,
                        EndSendHandler, null);
      }
    }

    protected void SendNotification(EndPoint ep)
    {
      object[] response = new object[2];
      response[0] = (int) _ptype.Notification;
      IList tas = _node.LocalTAs;
      string[] tas_string = new string[tas.Count];
      for(int i = 0; i < tas.Count; i++) {
        tas_string[i] = tas[i].ToString();
      }
      response[1] = tas_string;
      using(MemoryStream ms = new MemoryStream()) {
        int length = AdrConverter.Serialize(response, ms);
        byte[] buffer = ms.GetBuffer();
        _uc.BeginSendTo(buffer, 0, length, 0, ep, 
                        EndSendHandler, null);
      }
    }

    protected void EndSendHandler(IAsyncResult asr)
    {
      _uc.EndSendTo(asr);
    }
  }
}
