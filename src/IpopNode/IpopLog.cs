/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using System.Diagnostics;

namespace Ipop {
  /// <summary>Logging class for Ipop, still use ProtocolLog to write</summary>
  public class IpopLog {
    /// <summary>Turns on very low amount of debug code.</summary>
    public static BooleanSwitch BaseLog =
        new BooleanSwitch("BaseLog", "Weak level of logging");
    /**  <summary>Prints the source and destination of all incoming and
    outgoing packets</summary>*/
    public static BooleanSwitch PacketLog =
        new BooleanSwitch("PacketLog", "Logs incoming and outgoing packets");
    /// <summary>Prints DHCP information.</summary>
    public static BooleanSwitch DHCPLog = 
        new BooleanSwitch("DHCPLog", "Logs DHCP state data");
    /// <summary>Prints Resolver information.</summary>
    public static BooleanSwitch ResolverLog =
        new BooleanSwitch("ResolverLog", "Logs routing data");
    /// <summary>Prints DNS information.</summary>
    public static BooleanSwitch DNS =
        new BooleanSwitch("DNS", "All about DNS");
    /// <summary>Prints ARP information.</summary>
    public static BooleanSwitch ARP =
        new BooleanSwitch("ARP", "All about ARP");
    /// <summary>Prints TAP Info.</summary>
    public static BooleanSwitch TapLog =
        new BooleanSwitch("TapLog", "All about Tap");
    public static BooleanSwitch GroupVPN =
        new BooleanSwitch("GroupVPN", "All about GroupVPN");
  }
}
