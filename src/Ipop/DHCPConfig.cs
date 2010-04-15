/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Xml;
using System.Xml.Serialization;
using System.IO;

namespace Ipop {
  /// <summary>A Configuration class for the DHCP Lease Controller</summary>
  public class DHCPConfig {
    /// <summary>The length of a lease.</summary>
    public int LeaseTime;
    /// <summary>The name of the IPOP Namespace.</summary>
    public string Namespace;
    /// <summary>The netmask for the IPOP Namespace.</summary>
    public string Netmask;
    /// <summary>Base IP Address.</summary>
    public string IPBase;
    /// <summary>An array of reserved IP Addresses.</summary>
    public ReservedIP[] ReservedIPs;

    /// <summary>Specifies reserved IP cases</summary>
    public class ReservedIP {
      /// <summary>The address to reserve.</summary>
      public string IPBase;
      /// <summary>The mask to use.</summary>
      public string Mask;
    }
  }
}
