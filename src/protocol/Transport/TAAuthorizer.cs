/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2006  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet {

/**
 * When we want to allow or deny communications to a particular
 * TransportAddress implementations of this object handle this
 */
abstract public class TAAuthorizer {

  /**
   * @return the Decision for this TransportAddress
   */
  public virtual Decision Authorize(TransportAddress a) {
    return Decision.None;
  }

  /**
   * At the end of the day, we will often allow as long as
   * an address is not denied (@see SeriesTAAuthorizer),
   * so this makes it a little easier for us to check
   */
  public bool IsNotDenied(TransportAddress a) {
    return (Authorize(a) != Decision.Deny);
  }
  /**
   * There are three decisions:
   * Allow: the address is specifically allowed
   * Deny: the address is denied
   * None: this TAAuthorizer has nothing to say about this address
   */
  public enum Decision {
    Allow,
    Deny,
    None
  }
}

/**
 * This TAAuthorizer always denies or allows. 
 * It is useful to put in the
 * end of a SeriesTaAuthorizer to be strict or loose
 */
public class ConstantAuthorizer : TAAuthorizer {
  protected TAAuthorizer.Decision _dec;

  public ConstantAuthorizer(TAAuthorizer.Decision d) {
    _dec = d;
  }
  public override TAAuthorizer.Decision Authorize(TransportAddress a) {
    return _dec;
  }
}

/**
 * Given a list of TAAuthorizer objects, this goes
 * through until it gets an explicit Allow or Deny.  Otherwise
 * it returns None
 */
public class SeriesTAAuthorizer : TAAuthorizer {

  protected IEnumerable _authorizers;
  
  /**
   * Don't change auths after passing it to this constructor
   * or you are in for a world of hurt.
   */
  public SeriesTAAuthorizer(IEnumerable auths) {
    _authorizers = auths;
  }

  /**
   * Go through the list of Authorizers returning the first decision
   */
  public override TAAuthorizer.Decision Authorize(TransportAddress a) {
    TAAuthorizer.Decision result = TAAuthorizer.Decision.None;
    foreach(TAAuthorizer ipa in _authorizers) {
      result = ipa.Authorize(a);
      if( result != TAAuthorizer.Decision.None ) {
        break;
      }
    }
    return result;
  }
}

  /**
     Denies any attempt on a particular port.
     @param port port to which edge creation attempt is denied
  */
 
public class PortTAAuthorizer : TAAuthorizer {
  protected int _denied_port;
  public PortTAAuthorizer(int port) {
    _denied_port = port;
  }
  public override TAAuthorizer.Decision Authorize(TransportAddress a) {
    if (_denied_port == ((IPTransportAddress) a).Port) {
      return TAAuthorizer.Decision.Deny;
    } else {
      //else this decision should not matter
      return TAAuthorizer.Decision.None;
    }
  }
}

  /** 
      Denies a random TA and remembers it for subsequent denials.
   */

public class RandomTAAuthorizer: TAAuthorizer {
  protected static Random _rand = new Random();
  protected ArrayList _deny_list;
  protected double  _deny_prob;
  public RandomTAAuthorizer(double deny_prob) {
    _deny_list = new ArrayList();
    _deny_prob = deny_prob;
  }
  public override TAAuthorizer.Decision Authorize(TransportAddress a) {
    if (_deny_list.Contains(a)) {
      return TAAuthorizer.Decision.Deny;
    }
    // randomly deny the TA
    if (_rand.NextDouble() > _deny_prob) {
      _deny_list.Add(a);
      return TAAuthorizer.Decision.Deny;      
    }
    return TAAuthorizer.Decision.Allow;
  }  
}

public class NetmaskTAAuthorizer : TAAuthorizer {

  /**
   * Given an IPAddress the first bit_c bits of the primary
   * TransportAddress IPAddress have to match to get the given
   * result
   * @param nw the Network we want to match
   * @param bit_c the number of initial bits of the network that must match.
   * @param on_match what happens when there is a match
   * @param on_mismatch what happens when there is a match
   *
   * Examples:
   * <ul>
   * <li>If you want to deny a certain netmask, but say nothing about another
   * use (Deny, None).</li>
   * <li>If you want to allow a certain netmask and deny all others:
   * use (Allow, Deny).  Note this mode could not be used in Series</li>
   * </ul> 
   * 
   */
  public NetmaskTAAuthorizer(IPAddress nw, int bit_c,
                                   TAAuthorizer.Decision on_match,
                                   TAAuthorizer.Decision on_mismatch) {
    _nw_bytes = nw.GetAddressBytes();
    _bit_c = bit_c;
    _result_on_match = on_match;
    _result_on_mismatch = on_mismatch;
  }
  protected byte[] _nw_bytes;
  protected int _bit_c;
  protected TAAuthorizer.Decision _result_on_match;
  protected TAAuthorizer.Decision _result_on_mismatch;

  public override TAAuthorizer.Decision Authorize(TransportAddress a) {
    IPAddress ipa = (IPAddress)( ((IPTransportAddress) a).GetIPAddresses()[0] );
    byte[] add_bytes = ipa.GetAddressBytes();
    int bits = _bit_c;
    int block = 0;
    bool match = true;
    while( bits > 0 && match ) {
      match = FirstBitsMatch( add_bytes[block],  _nw_bytes[block], bits);
      bits -= 8;
      block++;
    }
    if( match ) {
      return _result_on_match;
    }
    else {
      return _result_on_mismatch;
    }
  }
  protected bool FirstBitsMatch(byte a, byte b, int count) {
    byte mask = 0;
    if( count >= 8 ) { mask = 0xFF; }
    else {
     switch( count ) {
      //This is the most common case, put it first
      case 8:
        mask = 0xFF;
        break;
      case 1:
        mask = 0x80;
        break;
      case 2:
        mask = 0xC0;
        break;
      case 3:
        mask = 0xE0;
        break;
      case 4:
        mask = 0xF0;
        break;
      case 5:
        mask = 0xF8;
        break;
      case 6:
        mask = 0xFC;
        break;
      case 7:
        mask = 0xFE;
        break;
      default:
        mask = 0xFF;
        break;
     }
    }
    //Now we have the mask:
    int ai = a & mask;
    int bi = b & mask;
    return (ai == bi);
  }
}

#if BRUNET_NUNIT
[TestFixture]
public class AuthorizerTester {
  [Test]
  public void Test() {
    TAAuthorizer a1 = new ConstantAuthorizer(TAAuthorizer.Decision.Allow);
    TransportAddress ta = TransportAddressFactory.CreateInstance("brunet.udp://127.0.0.1:45");
    Assert.IsTrue( a1.IsNotDenied( ta ), "constant allow");
    TAAuthorizer a2 = new ConstantAuthorizer(TAAuthorizer.Decision.Deny);
    Assert.IsFalse( a2.IsNotDenied( ta ), "constant deny");
    
    IPAddress network = IPAddress.Parse("10.128.0.0");
    TAAuthorizer a3 = new NetmaskTAAuthorizer(network, 9,
                                              TAAuthorizer.Decision.Deny,
                                              TAAuthorizer.Decision.None);
    TransportAddress ta2 = TransportAddressFactory.CreateInstance("brunet.udp://10.255.255.255:80");
    Assert.AreEqual(a3.Authorize(ta2), TAAuthorizer.Decision.Deny, "Netmask Deny");
    TransportAddress ta3 = TransportAddressFactory.CreateInstance("brunet.udp://10.1.255.255:80");
    Assert.AreEqual(a3.Authorize(ta3), TAAuthorizer.Decision.None, "Netmask None");
    //Here is the series:
    //If Netmask doesn't say no, constant says yes:
    TAAuthorizer[] my_auths = new TAAuthorizer[]{ a3, a1 };
    TAAuthorizer a4 = new SeriesTAAuthorizer(my_auths);
    Assert.AreEqual(a4.Authorize(ta2), TAAuthorizer.Decision.Deny, "Series Deny");
    Assert.AreEqual(a4.Authorize(ta3), TAAuthorizer.Decision.Allow, "Series Allow");
  }

}

#endif

}
