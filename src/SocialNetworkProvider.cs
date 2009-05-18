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

    protected readonly SocialUser _local_user;

    protected readonly byte[] _local_cert_data;

    protected readonly Dht _dht;

    protected readonly Dictionary<string, IProvider> _providers;

    protected readonly Dictionary<string, ISocialNetwork> _networks;

    public SocialNetworkProvider(Dht dht, SocialUser user, byte[] certData) {
      _local_user = user;
      _dht = dht;
      _providers = new Dictionary<string, IProvider>();
      _networks = new Dictionary<string,ISocialNetwork>();
      _local_cert_data = certData;
      RegisterBackends();
    }

    public void RegisterBackends() {
      TestNetwork google_backend = new TestNetwork(_local_user,
                                                   _local_cert_data);
      _providers.Add("GoogleBackend", google_backend);
      _networks.Add("GoogleBackend", google_backend);
    }

    public bool Login(string id, string username, string password) {
      bool provider_login = true;
      bool network_login = true;
      if(_providers.ContainsKey(id)) {
        provider_login = _providers[id].Login(id, username, password);
      }
      if(_networks.ContainsKey(id)) {
        network_login = _networks[id].Login(id, username, password);
      }
      return (provider_login && network_login);
    }

    public List<string> GetFriends() {
      List<string> friends = new List<string>();
      foreach(ISocialNetwork network in _networks.Values) {
        List<string> tmp_friends = network.GetFriends();
        if(tmp_friends == null) {
          continue;
        }
        foreach(string friend in tmp_friends) {
          if(friend != "" || !friends.Contains(friend)) {
            friends.Add(friend);
          }
        }
      }
      return friends;
    }

    public List<string> GetFingerprints(string[] uids) {
      List<string> fingerprints = new List<string>();
      foreach(IProvider provider in _providers.Values) {
        List<string> tmp_fprs = provider.GetFingerprints(uids);
        if(tmp_fprs == null) {
          continue;
        }
        foreach(string fpr in tmp_fprs) {
          if(fpr != "" || !fingerprints.Contains(fpr)) {
            fingerprints.Add(fpr);
          }
        }
      }
      return fingerprints;
    }

    public List<byte[]> GetCertificates(string[] uids) {
      List<byte[]> certificates = new List<byte[]>();
      foreach(IProvider provider in _providers.Values) {
        List<byte[]> tmp_certs = provider.GetCertificates(uids);
        if(tmp_certs == null) {
          continue;
        }
        foreach(byte[] cert in tmp_certs) {
          if(!certificates.Contains(cert)) {
            certificates.Add(cert);
          }
        }
      }
      return certificates;
    }

    public bool StoreFingerprint() {
      bool success = false;
      foreach(IProvider provider in _providers.Values) {
        success = (success || provider.StoreFingerprint());
      }
      return success;
    }

    public bool ValidateCertificate(byte[] certData) {
      foreach(IProvider provider in _providers.Values) {
        if(provider.ValidateCertificate(certData)) {
          return true;
        }
      }
      return false;
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
