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

  /**
   * This class manages all of the social networks and identity providers.
   * Additional social networks are registers in the register backends method.
   */
  public class SocialNetworkProvider : IProvider, ISocialNetwork {

    /**
     * The local SocialUser.
     */
    protected readonly SocialUser _local_user;

    /**
     * The byte array for the local certificate.
     */
    protected readonly byte[] _local_cert_data;

    /**
     * Dht object used to store data in P2P data store.
     */
    protected readonly Dht _dht;

    /**
     * The list of identity providers.
     */
    protected readonly Dictionary<string, IProvider> _providers;

    /**
     * The list of social networks.
     */
    protected readonly Dictionary<string, ISocialNetwork> _networks;

    /**
     * Constructor.
     * @param dht the dht object.
     * @param user the local user object.
     * @param certData the local certificate data.
     */
    public SocialNetworkProvider(Dht dht, SocialUser user, byte[] certData) {
      _local_user = user;
      _dht = dht;
      _providers = new Dictionary<string, IProvider>();
      _networks = new Dictionary<string,ISocialNetwork>();
      _local_cert_data = certData;
      RegisterBackends();
    }

    /**
     * Registers the various socialvpn backends.
     */
    public void RegisterBackends() {
      TestNetwork google_backend = new TestNetwork(_local_user,
                                                   _local_cert_data);
      _providers.Add("GoogleBackend", google_backend);
      _networks.Add("GoogleBackend", google_backend);
    }

    /**
     * Login method to sign in a particular backend.
     * @param id the identifier for the backend.
     * @param the username for the backend.
     * @param the password for the backend.
     * @return a boolean indicating success or failure.
     */
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

    /**
     * Get a list of friends from the various backends.
     * @return a list of friends uids.
     */
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

    /**
     * Get a list of fingerprints from backends.
     * @param uids a list of friend's uids.
     * @return a list of fingerprints.
     */
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

    /**
     * Get a list of certificates.
     * @param uids a list of friend's uids.
     * @return a list of certificates.
     */
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

    /**
     * Stores the fingerprint of local user.
     * @return boolean indicating success.
     */
    public bool StoreFingerprint() {
      bool success = false;
      foreach(IProvider provider in _providers.Values) {
        success = (success || provider.StoreFingerprint());
      }
      return success;
    }

    /**
     * Validates a certificate
     * @param certData the certificate data.
     * @return boolean indicating success.
     */
    public bool ValidateCertificate(byte[] certData) {
      foreach(IProvider provider in _providers.Values) {
        if(provider.ValidateCertificate(certData)) {
          return true;
        }
      }
      // TODO - Statement below should be false
      return true;
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
