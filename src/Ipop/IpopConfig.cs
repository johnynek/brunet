/*
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
using System.Text;
using System.Collections;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Ipop {
  /// <summary>IpopConfig contains configuration data meant for IPOP.</summary>
  public class IpopConfig {
    /**  <summary>Used to provide parallel dimension of IP space in a single
    overlay system</summary>*/
    public string IpopNamespace;
    /// <summary>The device name of the TAP device</summary>
    public string VirtualNetworkDevice;
    /// <summary>End Point Mapping.</summary>
    public AddressInfo AddressData;
    /// <summary>Enable Multicast</summary>
    public bool EnableMulticast;
    /// <summary>Enables End-To-End Security</summary>
    public bool EndToEndSecurity;
    /// <summary>DHCP base port (67 default)</summary>
    public int DHCPPort;
    /// <summary>Allow static addresses</summary>
    public bool AllowStaticAddresses;
    /// <summary>DNS Configuration Setting, default none</summary>
    public DNSInfo DNS;
    /// <summary>GroupVPN configuration settings, default none</summary>
    public GroupVPNInfo GroupVPN;

    /// <summary>AddressInfo stores end point mappings depending on the system
    /// all or none of these need to be defined before run time, this is here
    /// to save configuration for future use.</summary>
    public class AddressInfo {
      /// <summary>The hostname to associate to the IP.</summary>
      public string Hostname;
      /// <summary>Last IP address for this node.</summary>
      public string IPAddress;
      /// <summary>Last netmask for this node.</summary>
      public string Netmask;
      /**  <summary>Not implemented, but should contain last ethernet address
      for the node</summary>*/
      public string EthernetAddress;
    }

    public class DNSInfo {
      /// <summary> A supported DNS type </summary>
      public string Type;
      /// <summary> Enables IPOP DNS to forward DNS queries to specified NameServer </summary>
      public bool ForwardQueries;
      /// <summary> The NameServer to be used when Forward Queries is set true </summary>
      public string NameServer;
    }

    public class GroupVPNInfo {
      public bool Enabled;
      public string ServerURI;
      public string Group;
      public string UserName;
      public string Secret;
    }

    /// <summary>Path to the configs file system location.</summary>
    [NonSerialized]
    public string Path;

    /// <summary>Writres the config to the file system.</summary>
    public bool WriteConfig() {
      if(Path == string.Empty) {
        return false;
      }

      using(FileStream fs = new FileStream(Path, FileMode.Create, FileAccess.Write)) {
        XmlSerializer serializer = new XmlSerializer(this.GetType());
        serializer.Serialize(fs, this);
      }
      return true;
    }
  }
}
