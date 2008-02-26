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

using System;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;

namespace Brunet.Applications {
  /**
   * The class configuration for BasicNode.  brunet1/config/ has some sample
   * xml versions.
   */
  public class NodeConfig {
    public String BrunetNamespace; /**< Also known as Node.Realm different 
                                   namespaces can not communicate. */
    [XmlArrayItem (typeof(String), ElementName = "Transport")]
    public String[] RemoteTAs; /**< List of well known remote end points in 
      the form of brunet.(udp,tcp)://ip_address:port */
    public EdgeListener[] EdgeListeners; /**< Local end points */
    public String NodeAddress; /**< A stored Node.Address for re-use */
    [XmlArrayItem (typeof(String), ElementName = "Device")]
    public String[] DevicesToBind; /**< A list of network devices to use as
      end points on this machine, it reduces the amount of advertised end
      points */
    public Service RpcDht; /**< Specifies whether or not to start RpcDht */
    public Service XmlRpcManager; /**< Specifies whether or not to start the
                                  XmlRpc Services */

    /**
    * Used by services to specify if they are enabled and their port
    */
    public class Service {
      public bool Enabled; /**< Is the service enabled? */
      public int Port; /**< Which port should we run it on? */
    }

    /**
    * Used by EdgeListener's to specify their type, udp, tcp, etc and
    * optionally a port
    */
    public class EdgeListener {
      [XmlAttribute]
      public String type; /**< type such as tcp and udp */
      public int port; /**< a port 0 or empty for random */
    }
  }
}
