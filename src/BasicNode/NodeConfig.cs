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
using System.Text;
using System.Collections;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Ipop {
  public class NodeConfig {
    public String BrunetNamespace;
    [XmlArrayItem (typeof(String), ElementName = "Transport")]
    public String[] RemoteTAs;
    public EdgeListener[] EdgeListeners;
    public String NodeAddress;
    [XmlArrayItem (typeof(String), ElementName = "Device")]
    public String[] DevicesToBind;
    public Service RpcDht;
    public Service XmlRpcManager;
  }

  public class Service {
    public bool Enabled;
    public int Port;
  }

  public class EdgeListener {
    [XmlAttribute]
    public String type;
    public int port;
  }

  public class NodeConfigHandler {
    public static NodeConfig Read(String path) {
      XmlSerializer serializer = new XmlSerializer(typeof(NodeConfig));
      NodeConfig config = null;
      using(FileStream fs = new FileStream(path, FileMode.Open)) {
        config = (NodeConfig) serializer.Deserialize(fs);
      }
      return config;
    }

    public static void Write(String path, NodeConfig config) {
      using(FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write)) {
        XmlSerializer serializer = new XmlSerializer(typeof(NodeConfig));
        serializer.Serialize(fs, config);
      }
    }
  }
}
