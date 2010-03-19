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
using System.Collections.Specialized;

#if BRUNET_NUNIT
using System.Security.Cryptography;
using NUnit.Framework;
using Brunet.Symphony;
using Brunet.Transport;
#endif

namespace Brunet.Connections
{

  /**
   * Link messages are exchanged between hosts as
   * part of a connection forming handshake.
   * The local and remote transport
   * addresses are exchanged in order to help nodes
   * identify when they are behind a NAT, which is
   * translating their IP addresses and ports.
   *
   *
   * This class is immutable
   */

  public class LinkMessage
  {

    public LinkMessage(ConnectionType t,
                       NodeInfo local,
                       NodeInfo remote,
                       string token)
    {
      _attributes = new StringDictionary();
      _attributes["type"] = Connection.ConnectionTypeToString(t);
      _local_ni = local;
      _remote_ni = remote;
      _token = token;
    }

    public LinkMessage(string connection_type, NodeInfo local, NodeInfo remote, string token)
    {
      _attributes = new StringDictionary();
      _attributes["type"] = String.Intern( connection_type );
      _local_ni = local;
      _remote_ni = remote;
      _token = token;
    }
    public LinkMessage(StringDictionary attributes, NodeInfo local, NodeInfo remote, string token)
    {
      _attributes = attributes;
      _local_ni = local;
      _remote_ni = remote;
      _token = token;
    }
    public LinkMessage(IDictionary ht) {
      IDictionaryEnumerator en = ht.GetEnumerator();
      _attributes = new StringDictionary();
      while( en.MoveNext() ) {
        if( en.Key.Equals( "local" ) ) {
          IDictionary lht = en.Value as IDictionary;
          if( lht != null ) { _local_ni = NodeInfo.CreateInstance(lht); }
        }
        else if( en.Key.Equals( "remote" ) ) {
          IDictionary rht = en.Value as IDictionary;
          if( rht != null ) { _remote_ni = NodeInfo.CreateInstance(rht); }
        }
        else if (en.Key.Equals( "token" ) ) {
          _token = (string) ht["token"];
        }
        else {
          _attributes[ String.Intern( en.Key.ToString() ) ] = String.Intern( en.Value.ToString() );
        }
      }
    }

    /* These are attributes in the <link/> tag */
    /**
     * @returns the Main ConnectionType of this message.
     * @todo Make sure the usage of this is consistent
     */
    public ConnectionType ConnectionType {
      get { return Connection.StringToMainType( ConTypeString ); }
    }
    
    protected StringDictionary _attributes;
    public StringDictionary Attributes {
      get { return _attributes; }
    }
    public string ConTypeString { get { return _attributes["type"]; } }

    protected NodeInfo _local_ni;
    public NodeInfo Local {
      get { return _local_ni; }
    }

    protected NodeInfo _remote_ni;
    public NodeInfo Remote {
      get { return _remote_ni; } 
    }

    protected string _token;
    public string Token {
      get {
        return _token;
      }
    }
    /**
     * @return true if olm is equivalent to this
     */
    public override bool Equals(object olm)
    {
      LinkMessage lm = olm as LinkMessage;
      if ( lm != null ) {
        bool same = true;
	same &= (lm.Attributes.Count == Attributes.Count );
	same &= lm.ConTypeString == ConTypeString;
        same &= lm.Token.Equals(Token);
	if( same ) {
          //Make sure all the attributes match:
	  foreach(string key in lm.Attributes.Keys) {
            same &= lm.Attributes[key] == Attributes[key];
	  }
	}
	same &= lm.Local.Equals(_local_ni);
	same &= lm.Remote.Equals(_remote_ni);
	return same;
      }
      else {
        return false;
      }
    }
   
    public override int GetHashCode() {
      return _remote_ni.GetHashCode();
    }

    public IDictionary ToDictionary() {
      IDictionary ht = new ListDictionary();
      if( _local_ni != null ) {
        ht["local"] = _local_ni.ToDictionary();
      }
      if( _remote_ni != null ) {
        ht["remote"] = _remote_ni.ToDictionary();
      }
      if (_token != null) {
        ht["token"] = _token.ToString();
      }
      if( _attributes != null ) {
        foreach(DictionaryEntry de in _attributes) {
          ht[ de.Key ] = de.Value;
        }
      }
      return ht;
    }
  }

#if BRUNET_NUNIT
//Here are some NUnit 2 test fixtures
  [TestFixture]
  public class LinkMessageTester {

    public LinkMessageTester() { }
    
    public void RoundTripHT(LinkMessage lm) {
      LinkMessage lm2 = new LinkMessage( lm.ToDictionary() );
      Assert.AreEqual( lm, lm2, "LinkMessage HT Roundtrip" );
    }

    [Test]
    public void LMSerializationTest()
    {
      NodeInfo n1 = NodeInfo.CreateInstance(null, TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:45"));
      RandomNumberGenerator rng = new RNGCryptoServiceProvider();      
      AHAddress tmp_add = new AHAddress(rng);
      LinkMessage l1 = new LinkMessage(ConnectionType.Structured, n1,
				       NodeInfo.CreateInstance(new DirectionalAddress(DirectionalAddress.Direction.Left),
				       TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:837")), tmp_add.ToString() );
      RoundTripHT(l1);
      StringDictionary attrs = new StringDictionary();
      attrs["realm"] = "test_realm";
      attrs["type"] = "structured.near";
      LinkMessage l3 = new LinkMessage(attrs, n1, n1, tmp_add.ToString());
      RoundTripHT(l3);
    }
  }

#endif

}
