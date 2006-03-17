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
using System;

namespace Brunet {
  /** This class allows us to track from which destinations we 
      have been receiving packets. Only those are considered for ChotaConnections. 
  */
  public class ChotaConnectionIPPacketHandler: IAHPacketHandler {
    public event EventHandler ReceivePacketEvent;
    //default constructor
    public ChotaConnectionIPPacketHandler() {
    }
    public void HandleAHPacket(object node, AHPacket p, Edge from)
    {
#if ARI_CHOTA_DEBUG
      Console.WriteLine("ChotaConnectionIPPacketHandler: Got an IP packet. ");
#endif
      //raise an event into the subscriber
      if (ReceivePacketEvent != null) {
	ReceivePacketEvent(this, new SendPacketEventArgs(p));
      }
    }
  }
}
