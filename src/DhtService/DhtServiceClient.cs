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

using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;
using CookComputing.XmlRpc;
using Brunet.Dht;
using System;
using System.Collections;

namespace Brunet.Dht {
  public partial class DhtServiceClient {
    public static IXmlRpcDht GetXmlRpcDhtClient(int port) {
      IXmlRpcDht proxy = (IXmlRpcDht)XmlRpcProxyGen.Create(typeof(IXmlRpcDht));
      proxy.Url = "http://127.0.0.1:" + port + "/xd.rem";
      return proxy;
    }
  }

  /// <summary>
  /// The XmlRpc interface for the Dht which is used on the client side
  /// </summary>
  public interface IXmlRpcDht : IDht, CookComputing.XmlRpc.IXmlRpcProxy {
    [XmlRpcMethod]
    new DhtGetResult[] Get(string key);
    [XmlRpcMethod]
    new bool Create(string key, string value, int ttl);
    [XmlRpcMethod]
    new bool Put(string key, string value, int ttl);
    [XmlRpcMethod]
    new string BeginGet(string key);
    [XmlRpcMethod]
    new DhtGetResult ContinueGet(string token);
    [XmlRpcMethod]
    new void EndGet(string token);

    [XmlRpcMethod]
    new IDictionary GetDhtInfo();

    [XmlRpcBegin("Get")]
    IAsyncResult BeginGetWithCallback(string key, AsyncCallback acb, object state);
    [XmlRpcEnd]
    DhtGetResult[] EndGet(IAsyncResult iasr);
    [XmlRpcBegin("Create")]
    IAsyncResult BeginCreateWithCallback(string key, string value, int ttl, AsyncCallback acb, object state);
    [XmlRpcEnd]
    string EndCreate(IAsyncResult iasr);
    [XmlRpcBegin("Put")]
    IAsyncResult BeginPutWithCallback(string key, string value, int ttl, AsyncCallback acb, object state);
    [XmlRpcEnd]
    string EndPut(IAsyncResult iasr);
  }
}
