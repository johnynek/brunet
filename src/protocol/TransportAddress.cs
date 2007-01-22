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
                            string host, int port):
          base("brunet." + t.ToString().ToLower() + "://"
         + host + ":" + port.ToString())
    {
      _ips = null;
    }
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

    /**
     * Creates an IEnumerable of TransportAddresses for a fixed type and port,
     * over a list of IPAddress objects.
     */
    public static IEnumerable Create(TransportAddress.TAType tat, int port, IEnumerable ips)
    {
      ArrayList tas = new ArrayList();
      ArrayList back_list = null;
      foreach(IPAddress ip in ips) {
        /**
         * We add Loopback addresses to the back, all others to the front
         * This makes sure non-loopback addresses are listed first.
         */
        ArrayList l = tas;
        if( IPAddress.IsLoopback(ip) ) {
          //Put it at the back
          if( back_list == null ) { back_list = new ArrayList(); }
          l = back_list;
        }
        l.Add( new TransportAddress(tat, new IPEndPoint(ip, port) ) );
      }
      if (back_list != null ) {
        //Add all these to the end:
        foreach(object o in back_list) {
          tas.Add(o);
        }
      }
      return tas;
    }
    
    /**
     * This gets the name of the local machine, then does a DNS lookup on that
     * name, and finally does the same as TransportAddress.Create for that
     * list of IPAddress objects.
     *
     * If the DNS hostname is not correctly configured, it will return the
     * loopback address.
     */
    public static IEnumerable CreateForLocalHost(TransportAddress.TAType tat, int port) {
      try {
        string StrLocalHost = Dns.GetHostName();
        IPHostEntry IPEntry = Dns.GetHostByName(StrLocalHost);
        return Create(tat, port, IPEntry.AddressList);
      }
      catch(Exception) {
        //Oh, well, that didn't work.
        ArrayList tas = new ArrayList();
        //Just put the loopback address, it might help us talk to some other
        //local node.
        tas.Add( new TransportAddress(tat, new IPEndPoint(IPAddress.Loopback, port) ) );
        return tas;
      }
 
    }
  }

}
