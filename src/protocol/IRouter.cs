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
 * Brunet.Address
 * Brunet.AHPacket
 * Brunet.Edge
 * Brunet.ConnectionType
 * Brunet.ConnectionTable
 */

namespace Brunet
{

  /**
   * Different Address classes are routed differently.
   * There are different Router classes for each different
   * routing rule.  They each implement this interface
   */

  public interface IRouter
  {

    /**
     * @return the Type of the Address this router supports 
     */
    System.Type RoutedAddressType
    {
      get;
      }

      ConnectionTable ConnectionTable
      {
        set;
        }

        /**
         * @param p the AHPacket to route
         * @param from The edge the packet came from
         * @param deliverlocally set to true if the local node should Announce it
         * @return the number of edges the packet it Sent on.
         */
        int Route(Edge from, AHPacket p, out bool deliverlocally);
  }

}
