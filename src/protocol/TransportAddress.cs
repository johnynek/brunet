/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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
using System.Net;

namespace Brunet
{

  /**
   * Represents the addresses used to transport the Brunet
   * protocol over lower layers (such as IP).  The transport
   * address is used when one host wants to connect to another
   * host in order to route Brunet packets.
   */

  public class TransportAddress:System.Uri, IComparable
  {

    protected ArrayList _ips=null;

    public enum TAType
    {
      Unknown,
      Tcp,
      Udp,
      Function,
      Tls,
      TlsTest
    }

    public TAType TransportAddressType
    {
      get {
        string t = Scheme.Substring(Scheme.IndexOf('.') + 1);
        return (TAType) System.Enum.Parse(typeof(TAType), t, true);
      }
    }

    public int CompareTo(object ta)
    {
      if (ta is TransportAddress) {
        ///@todo it would be nice to do a comparison that is not string based:
        return this.ToString().CompareTo(ta.ToString());
      }
      else {
        return -1;
      }
    }

    public TransportAddress(string uri):base(uri) { }
    public TransportAddress(TransportAddress.TAType t,
                            System.Net.IPAddress add, int port):
          base("brunet." + t.ToString().ToLower() + "://"
         + add.ToString() + ":" + port.ToString())
    {
      _ips = new ArrayList();
      _ips.Add( add );
    }
    public TransportAddress(TransportAddress.TAType t,
                            System.Net.IPEndPoint ep) :
    this(t, ep.Address, ep.Port) {
    }

    public ArrayList GetIPAddresses()
    {
      if ( _ips != null ) {
        return _ips;
      }

      try {
        IPAddress a = IPAddress.Parse(Host);
        _ips = new ArrayList();
        _ips.Add(a);
        return _ips;
      }
      catch(Exception) {

      }

      try {
        IPHostEntry IPHost = Dns.Resolve(Host);
        _ips = new ArrayList(IPHost.AddressList);
      } catch(Exception e) {
        // log this exception!
	System.Console.Error.WriteLine("In GetIPAddress() Resolving {1}: {0}",
                                        e, Host);
      }
      return _ips;
    }

  }

}
