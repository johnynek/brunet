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
using Ipop;
using Brunet.DistributedServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Ipop.DhtNode {
  /**
  <summary>This class provides the ability to lookup names using the Dht.  To
  add a name into the Dht either add a Hostname node into your AddressData node
  inside the IpopConfig or use another method to publish to the Dht.  The format
  of acceptable hostnames is [a-zA-Z0-9-_\.]*.ipop (i.e. must end in
  .ipop)</summary>
  */
  public class DhtDNS: DNS {
    /// <summary>Maps names to IP Addresses</summary>
    protected Cache dns_a = new Cache(100);
    /// <summary>Maps IP Addresses to names</summary>
    protected Cache dns_ptr = new Cache(100);
    /// <summary>Use this Dht to resolve names that aren't in cache</summary>
    protected IDht _dht;
    /// <summary>The namespace where the hostnames are being stored.</summary>
    protected String _ipop_namespace;

    /**
    <summary>Create a DhtDNS using the specified Dht object</summary>
    <param name="dht">A Dht object used to acquire name translations</param>
    */
    public DhtDNS(IDht dht, String ipop_namespace) {
      _ipop_namespace = ipop_namespace;
      _dht = dht;
    }

    /**
    <summary>Called during LookUp to perform translation from hostname to IP.
    If an entry isn't in cache, we can try to get it from the Dht.  Throws
    an exception if the name is invalid and returns null if no name is found.
    </summary>
    <param name="name">The name to lookup</param>
    <returns>The IP Address or null if none exists for the name.  If the name
    is invalid, it will throw an exception.</returns>
     */
    public override String AddressLookUp(String name) {
      if(!name.EndsWith(DomainName)) {
        throw new Exception("Invalid DNS name: " + name);
      }
      String ip = (String) dns_a[name];
      if(ip == null) {
        try {
          ip = Encoding.UTF8.GetString((byte[]) _dht.Get(Encoding.UTF8.GetBytes(_ipop_namespace + "." + name))[0]["value"]);
          if(ip != null) {
            lock(_sync) {
              dns_a[name]= ip;
              dns_ptr[ip] = name;
            }
          }
        }
        catch{}
      }
      return ip;
    }

    /**
    <summary>Called during LookUp to perfrom a translation from IP to hostname.
    Entries get here via the AddressLookUp as the Dht does not retain pointer
    lookup information.</summary>
    <param name="IP">The IP to look up.</param>
    <returns>The name or null if none exists for the IP.</returns>
    */
    public override String NameLookUp(String IP) {
      return (String) dns_ptr[IP];
    }
  }
}
