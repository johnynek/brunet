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

/**
 * Dependencies : 
 * Brunet.AHAddress
 * Brunet.AHRouter
 * Brunet.AHPacket
 * Brunet.Address
 * Brunet.CtmRequestHandler
 * Brunet.ConnectionOverlord
 * Brunet.ConnectionType
 * Brunet.DirectionalRouter
 * Brunet.Edge
 * Brunet.LeafConnectionOverlord
 * Brunet.Linker
 * Brunet.IAHPacketHandler
 * Brunet.Node
 * Brunet.PacketForwarder
 * Brunet.StructuredConnectionOverlord
 */

#define PRODUCTION
//to run the connecttester, make sure you change PRODUCTION to DEBUG

using System;
using System.Collections;
//using log4net;

namespace Brunet
{

  /**
   * A node that only makes connections on the structured system
   * and only routes structured address packets.
   */

  public class StructuredNode:Node
  {
    /*private static readonly log4net.ILog log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/


    /**
     * Here are the ConnectionOverlords for this type of Node
     */
    protected ConnectionOverlord _lco;
    protected ConnectionOverlord _sco;

    public StructuredNode(AHAddress add):base(add)
    {
      /**
       * Here are the routers this node uses : 
       */
      ArrayList routers = new ArrayList();
      routers.Add(new AHRouter(add));
      routers.Add(new DirectionalRouter(add));
      SetRouters(routers);

      /**
       * Here are the ConnectionOverlords
       */ 
      _lco = new LeafConnectionOverlord(this);
      _sco = new StructuredConnectionOverlord(this);

      /**
       * Turn on some protocol support : 
       */
      /// Turn on Packet Forwarding Support :
      IAHPacketHandler h = new PacketForwarder(add);
      Subscribe(AHPacket.Protocol.Forwarding, h);
      /**
       * Here is how we handle ConnectToMessages : 
       */
      h = new CtmRequestHandler();
      Subscribe(AHPacket.Protocol.Connection, h);
    }

    /**
     * Connect to the network.  This informs all the ConnectionOverlord objects
     * to do their thing.
     */
    override public void Connect()
    {
      StartAllEdgeListeners();

      _lco.IsActive = true;
      _sco.IsActive = true;

      _lco.Activate();
      _sco.Activate();
    }
    /**
     * This informs all the ConnectionOverlord objects
     * to not respond to loss of edges, then to issue
     * close messages to all the edges
     * 
     */
    override public void Disconnect()
    {
      _lco.IsActive = false;
      _sco.IsActive = false;

      //Gracefully close all the edges:
      ArrayList edges_to_close = new ArrayList();
      lock( _connection_table.SyncRoot ) {
        foreach(Connection c in _connection_table) {
          edges_to_close.Add( c.Edge );
        }
      }
      foreach(Edge e in edges_to_close) {
        GracefullyClose(e);
      }
      // stop all edge listeners to prevent other nodes
      // from connecting to us
      StopAllEdgeListeners();
    }
  }

}


