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
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Lifetime;
using CookComputing.XmlRpc;
using System.Threading;
using Brunet;
using Brunet.DistributedServices;

namespace Brunet.DhtService {
  /// <summary>
  /// A Dht Server thread listens to Soap and XmlRpc requests
  /// Soap URL: http://localhost:port/sd.rem
  /// XmlRpc URL: http://localhost:port/xd.rem
  /// </summary>
  public class DhtServer {
    private DhtAdapter _sd, _xd;
    protected IChannel _channel;
    public  DhtServer(int port) {
      if(port == 0) {
        throw new Exception("Must be started with a valid specific port number!");
      }
      IServerChannelSinkProvider chain = new XmlRpcServerFormatterSinkProvider();
      chain.Next = new SoapServerFormatterSinkProvider();

      IDictionary props = new Hashtable();
      props.Add("port", port);
      props.Add("name", "dhtsvc");
      _channel = new HttpChannel(props, null, chain);
      ChannelServices.RegisterChannel(_channel, false);
    }

    public void Stop(){
      try { Suspend(); } catch {}
      try {
        ChannelServices.UnregisterChannel(_channel);
      } catch{}
    }

    public void Suspend() { 
      RemotingServices.Disconnect(_sd);
      RemotingServices.Disconnect(_xd);
    }

    public void Update(IDht dht) {
      _sd = new SoapDht(dht);
      RemotingServices.Marshal(_sd, "sd.rem");

      _xd = new XmlRpcDht(dht);
      RemotingServices.Marshal(_xd, "xd.rem");
    }
  }
}
