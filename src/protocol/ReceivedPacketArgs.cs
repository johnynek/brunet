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

//using Brunet.Packet;
using Brunet;

namespace Brunet
{

  /**
   * The .Net event model suggests using System.EventHandler
   * for all events, but subclassing the System.EventArgs.
   * This class represents the arguments for the Event
   * which occurs when a packet arrives.
   */

  public class ReceivedPacketArgs:System.EventArgs
  {

    public Brunet.Packet packet;

    public ReceivedPacketArgs(Brunet.Packet p)
    {
      this.packet = p;
    }
  }

}
