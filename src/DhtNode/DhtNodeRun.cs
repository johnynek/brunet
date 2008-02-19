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

namespace Ipop {
  public class DhtNodeRun: BasicNode {
    protected String _ipop_config_path;

    public static void Main(String[] args) {
      DhtNodeRun node = new DhtNodeRun(args[0], args[1]);
      node.Run();
    }

    public DhtNodeRun(String NodeConfigPath, String IpopConfigPath): 
      base(NodeConfigPath) {
      _ipop_config_path = IpopConfigPath;
    }

    public override void Run() {
      CreateNode();
      new DhtIpopNode(_node, _dht, _ipop_config_path);
      _node.Connect();
      StopServices();
    }
  }
}