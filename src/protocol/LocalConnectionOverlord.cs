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
    private Node _node;
    private DateTime _last_call;
    public static readonly string NOTIFY = "lco_notify";
    public static readonly string REQUEST = "lco_request";

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

    public LocalConnectionOverlord(Node node) {
      _node = node;
      _node.ZeroConf.Register(NOTIFY, HandleNotify);
      _node.ZeroConf.Register(REQUEST, HandleRequest);
    }

    /**
     * If IsActive, then start trying to get connections.
     */
    public override void Activate() {
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

    /**
     * Do we need to use this to get connections?
     */
    public void CheckConnection(object o, EventArgs ea)
    {
      if(!_active || !NeedConnection) {
        return;
      }
      DateTime now = DateTime.UtcNow;
      if(now - _last_call > TimeSpan.FromSeconds(60)) {
        _last_call = now;
        Announce();
      }
    }

    /**
     * Used to request other nodes existence
     */
    protected void Announce()
    {
      _node.ZeroConf.Announce(REQUEST, null);
    }

    /**
     * When we receive a new Notify message, we check each entry to make
     * sure that all TransportAddresses are indeed IPTransportAddresses.
     */
    protected void HandleNotify(EndPoint ep, IList tas)
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

    /**
     * Send a list of our TAs in string format to the specified end point.
     */
    protected void HandleRequest(EndPoint ep, IList empty)
    {
      IList tas = (IList) ((ArrayList)_node.LocalTAs).Clone();
      string[] tas_string = new string[tas.Count];
      for(int i = 0; i < tas.Count; i++) {
        tas_string[i] = tas[i].ToString();
      }
      _node.ZeroConf.SendResponse(ep, NOTIFY, tas_string);
    }
  }
}
