/*
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
    /// <summary>Dns Configuration Setting, default none</summary>
    public DnsInfo Dns;
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

    public class DnsInfo {
      /// <summary> A supported Dns type </summary>
      public string Type;
      /// <summary> Enables IPOP Dns to forward Dns queries to specified NameServer </summary>
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
