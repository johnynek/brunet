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
      Function
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
         + add.ToString() + ":" + port.ToString()) { }
    public TransportAddress(TransportAddress.TAType t,
                            System.Net.IPEndPoint ep) :
    this(t, ep.Address, ep.Port) { }

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
      catch(Exception e) {

      }

      try {
        IPHostEntry IPHost = Dns.Resolve(Host);
        _ips = new ArrayList(IPHost.AddressList);
      } catch(Exception e) {
        // log this exception!
      }

      return _ips;
    }

  }

}
