/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
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
    /// <summary>Prints Dhcp information.</summary>
    public static BooleanSwitch DhcpLog = 
        new BooleanSwitch("DhcpLog", "Logs Dhcp state data");
    /// <summary>Prints Resolver information.</summary>
    public static BooleanSwitch ResolverLog =
        new BooleanSwitch("ResolverLog", "Logs routing data");
    /// <summary>Prints Dns information.</summary>
    public static BooleanSwitch Dns =
        new BooleanSwitch("Dns", "All about Dns");
    /// <summary>Prints Arp information.</summary>
    public static BooleanSwitch Arp =
        new BooleanSwitch("Arp", "All about Arp");
    /// <summary>Prints TAP Info.</summary>
    public static BooleanSwitch TapLog =
        new BooleanSwitch("TapLog", "All about Tap");
    public static BooleanSwitch GroupVPN =
        new BooleanSwitch("GroupVPN", "All about GroupVPN");
  }
}
