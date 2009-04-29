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

    protected SocialUser _local_user;

    protected Dht _dht;

    protected IProvider _provider;

    protected ISocialNetwork _network;

    protected DrupalNetwork _drupal;

    public SocialNetworkProvider(Dht dht, SocialUser user) {
      _local_user = user;
      _dht = dht;
      _drupal = new DrupalNetwork(user);
      _provider = null;
      _network = null;
    }

    public bool Login(string username, string password) {
      _provider = _drupal;
      _network = _drupal;
      return _provider.Login(username, password);
    }

    public List<string> GetFriends() {
      if(_network != null) return _network.GetFriends();

      List<string> friends = new List<string>();
      friends.Add(_local_user.Uid);
      return friends;
    }

    public List<string> GetFingerprints(string uid) {
      if(_provider != null) return _provider.GetFingerprints(uid);

      List<string> fingerprints = new List<string>();
      DhtGetResult[] dgrs = null;

      try {
        dgrs = _dht.Get("svpn:" + uid);
      } catch (Exception e) {
        ProtocolLog.Write(SocialLog.SVPNLog,e.Message);
        ProtocolLog.Write(SocialLog.SVPNLog,"UID not found: " + uid);
      }
      foreach(DhtGetResult dgr in dgrs) {
        fingerprints.Add(dgr.valueString);
      }
      return fingerprints;
    }

    public bool StoreFingerprint() {
      if(_provider != null) return _provider.StoreFingerprint();

      string key = "svpn:" + _local_user.Uid;
      string value = _local_user.DhtKey;
      int ttl = 3600;  // in secs = one hour
      try {
        return _dht.Put(key, value, ttl);
      } catch (Exception e) {
        ProtocolLog.Write(SocialLog.SVPNLog,e.Message);
        ProtocolLog.Write(SocialLog.SVPNLog,"Dht Put failed for: " + key);
        return false;
      }
    }
  }
}
