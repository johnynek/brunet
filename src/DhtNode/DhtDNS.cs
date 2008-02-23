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

using Brunet;
using Brunet.DistributedServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Ipop {
  public class DhtDNS: DNS {
    public override String SUFFIX { get { return ".ipop_vpn"; } }
    protected Dht _dht;

    /*
     * We don't use the underlying hashtables, because caches ensure entries 
     * will eventually cycle and no worries of excessive memory consumption!
     */

    protected new Cache dns_a = new Cache(100);
    protected new Cache dns_ptr = new Cache(100);

    public DhtDNS(Dht dht) {
      _dht = dht;
    }

    public override String UnresolvedName(String qname) {
      try {
        String res = _dht.Get(qname)[0].valueString;
        lock(_sync) {
          dns_a[qname]= res;
        }
        return res;
      }
      catch {
        throw new Exception("Dht does not contain a record for " + qname);
      }
    }
  }
}
