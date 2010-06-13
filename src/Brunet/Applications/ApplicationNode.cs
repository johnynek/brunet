/*
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet.Security;
using Brunet.Security.PeerSec.Symphony;
using Brunet.Services.Coordinate;
using Brunet.Services.Dht;
using Brunet.Services.XmlRpc;
using Brunet.Symphony;

namespace Brunet.Applications {
  public class ApplicationNode {
    public readonly StructuredNode Node;
    public readonly IDht Dht;
    public readonly RpcDhtProxy DhtProxy;
    public readonly NCService NCService;
    public readonly SecurityOverlord SecurityOverlord;
    public readonly SymphonySecurityOverlord SymphonySecurityOverlord;
    public ApplicationNode PrivateNode;

    public ApplicationNode(StructuredNode node, IDht dht, RpcDhtProxy dht_proxy,
        NCService ncservice, SecurityOverlord security_overlord)
    {
      Node = node;
      Dht = dht;
      DhtProxy = dht_proxy;
      NCService = ncservice;
      SecurityOverlord = security_overlord;
      SymphonySecurityOverlord = security_overlord as SymphonySecurityOverlord;
    }
  }
}
