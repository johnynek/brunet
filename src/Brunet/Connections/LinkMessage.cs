/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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

  public class LinkMessage {
    public LinkMessage(ConnectionType con_type, NodeInfo local,
        NodeInfo remote, string realm, string token) :
      this(Connection.ConnectionTypeToString(con_type), local, remote, realm, token)
    {
    }

    public LinkMessage(string con_type, NodeInfo local, NodeInfo remote,
        string realm, string token)
    {
      Local = local;
      Remote = remote;
      Token = String.Intern(token);
      ConTypeString = String.Intern(con_type);
      Realm = String.Intern(realm);
    }

    public LinkMessage(IDictionary ht) {
      IDictionaryEnumerator en = ht.GetEnumerator();
      while( en.MoveNext() ) {
        if( en.Key.Equals( "local" ) ) {
          IDictionary lht = en.Value as IDictionary;
          if( lht != null ) { Local = NodeInfo.CreateInstance(lht); }
        } else if( en.Key.Equals( "remote" ) ) {
          IDictionary rht = en.Value as IDictionary;
          if( rht != null ) { Remote = NodeInfo.CreateInstance(rht); }
        } else if (en.Key.Equals( "token" ) ) {
          Token = String.Intern((string) en.Value);
        } else if (en.Key.Equals( "realm" ) ) {
          Realm = String.Intern((string) en.Value);
        } else if (en.Key.Equals( "type" ) ) {
          ConTypeString = String.Intern((string) en.Value);
        }
      }
    }

    public readonly NodeInfo Local;
    public readonly NodeInfo Remote;
    public readonly string ConTypeString;
    public readonly string Realm;
    public readonly string Token;

    /**
     * @returns the Main ConnectionType of this message.
     * @todo Make sure the usage of this is consistent
     */
    public ConnectionType ConnectionType {
      get { return Connection.StringToMainType( ConTypeString ); }
    }
    
    /**
     * @return true if olm is equivalent to this
     */
    public override bool Equals(object olm)
    {
      LinkMessage lm = olm as LinkMessage;
      bool same = false;
      if ( lm != null ) {
        same = lm.ConTypeString.Equals(ConTypeString);
        same &= lm.Token.Equals(Token);
        same &= lm.Remote.Equals(Remote);
        same &= lm.Local.Equals(Local);
        same &= lm.Realm.Equals(Realm);
      }
      return same;
    }
   
    public override int GetHashCode() {
      return Remote.GetHashCode();
    }

    public IDictionary ToDictionary() {
      IDictionary ht = new ListDictionary();
      if( Local != null ) {
        ht["local"] = Local.ToDictionary();
      }
      if( Remote != null ) {
        ht["remote"] = Remote.ToDictionary();
      }
      ht["token"] = Token;
      ht["realm"] = Realm;
      ht["type"] = ConTypeString;
      return ht;
    }
  }

#if BRUNET_NUNIT
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
               TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:837")), string.Empty,
               tmp_add.ToString() );
      RoundTripHT(l1);
    }
  }
#endif
}
