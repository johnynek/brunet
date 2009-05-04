/*
Copyright (C) 2009 Pierre St Juste <ptony82@ufl.edu>, University of Florida

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
using System.Collections.Generic;

using Brunet;
using Brunet.DistributedServices;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  public class SocialNetworkProvider : IProvider, ISocialNetwork {

    public const int DHTTTL = 3600;

    public const string DHTPREFIX = "svpn:";

    protected readonly SocialUser _local_user;

    protected readonly Dht _dht;

    protected readonly IProvider _provider;

    protected readonly ISocialNetwork _network;

    protected readonly DrupalNetwork _drupal;

    protected bool _online;

    public SocialNetworkProvider(Dht dht, SocialUser user) {
      _local_user = user;
      _dht = dht;
      _provider = _drupal;
      _network = _drupal;
      _drupal = new DrupalNetwork(user);
      _online = false;
    }

    public bool Login(string username, string password) {
      _online = true;
      return _provider.Login(username, password);
    }

    public List<string> GetFriends() {
      if(_online) return _network.GetFriends();

      List<string> friends = new List<string>();
      return friends;
    }

    public List<string> GetFingerprints(string uid) {
      if(_online) return _provider.GetFingerprints(uid);

      List<string> fingerprints = new List<string>();
      DhtGetResult[] dgrs = null;

      string key = DHTPREFIX + uid;
      try {
        dgrs = _dht.Get(key);
      } catch (Exception e) {
        ProtocolLog.Write(SocialLog.SVPNLog,e.Message);
        ProtocolLog.Write(SocialLog.SVPNLog,"DHT GET FPR FAILURE: " + key);
      }
      foreach(DhtGetResult dgr in dgrs) {
        fingerprints.Add(dgr.valueString);
      }
      return fingerprints;
    }

    public bool StoreFingerprint() {
      if(_online) return _provider.StoreFingerprint();

      string key = DHTPREFIX + _local_user.Uid;
      string value = _local_user.DhtKey;
      try {
        return _dht.Put(key, value, DHTTTL);
      } catch (Exception e) {
        ProtocolLog.Write(SocialLog.SVPNLog,e.Message);
        ProtocolLog.Write(SocialLog.SVPNLog,"DHT PUT FPR FAILURE: " + key);
        return false;
      }
    }
  }
#if SVPN_NUNIT
  [TestFixture]
  public class SocialNetworkProviderTester {
    [Test]
    public void SocialNetworkProviderTest() {
      Assert.AreEqual("test", "test");
    }
  } 
#endif
}
