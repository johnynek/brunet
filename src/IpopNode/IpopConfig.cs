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

    /**
    <summary>AddressInfo stores end point mappings depending on the system all
    or none of these need to be defined before run time, this is here to save
    configuration for future use.</summary>
    */
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

    /**
    <summary>Reads the specified config file and returns a IpopConfig</summary>
    <param name="configFile">the file to open including path</param>
    <returns> IpopConfig from the file or exception on error</returns>
    */
    public static IpopConfig Read(string configFile) {
      XmlSerializer serializer = new XmlSerializer(typeof(IpopConfig));
      IpopConfig config = null;
      using(FileStream fs = new FileStream(configFile, FileMode.Open)) {
        config = (IpopConfig) serializer.Deserialize(fs);
      }
      return config;
    }

    /**
    <summary>Writes the IpopConfig to the specified file.</summary>
    <param name="configFile"> the file to write to including path</param>
    <param name="config">IpopConfig data to be written to configFile</param>
    <returns>true on success, exception on failure</returns>
    */
    public static bool Write(string configFile, IpopConfig config) {
      using(FileStream fs = new FileStream(configFile, FileMode.Create, 
            FileAccess.Write)) {
        XmlSerializer serializer = new XmlSerializer(typeof(IpopConfig));
        serializer.Serialize(fs, config);
      }
      return true;
    }
  }
}
