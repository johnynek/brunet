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
 * Brunet.AHPacket
 * Brunet.Edge
 */

namespace Brunet
{

  /**
   * When objects want to handle packets delivered to local nodes
   * they implement this interface and Subscribe to particular
   * protocols on the Node
   */
  public interface IAHPacketHandler
  {

    /**
     * @param node The node that got the packet
     * @param p the packet
     * @param from the edge we got the packet from
     */
    void HandleAHPacket(object node, AHPacket p, Edge from);

    /**
     * @return true if you handle this type
     */
    bool HandlesAHProtocol(AHPacket.Protocol type);
  }

}
