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
   * Additional networks are registers in the register backends method.
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
     * Location of the certificates directory.
     */
    protected readonly string _cert_dir;

    /**
     * List of friends manually added.
     */
    protected readonly List<string> _friends;

    /**
     * List of fingerprints manually added.
     */
    protected readonly List<string> _fingerprints;

    /**
     * List of certificates manually added.
     */
    protected readonly List<byte[]> _certificates;

    /**
     * Constructor.
     * @param dht the dht object.
     * @param user the local user object.
     * @param certData the local certificate data.
     */
    public SocialNetworkProvider(Dht dht, SocialUser user, byte[] certData,
                                 string certDir) {
      _local_user = user;
      _dht = dht;
      _providers = new Dictionary<string, IProvider>();
      _networks = new Dictionary<string,ISocialNetwork>();
      _local_cert_data = certData;
      _cert_dir = certDir;
      _friends = new List<string>();
      _fingerprints = new List<string>();
      _certificates = new List<byte[]>();
      RegisterBackends();
      LoadCertificates();
    }

    /**
     * Registers the various socialvpn backends.
     */
    public void RegisterBackends() {
      TestNetwork google_backend = new TestNetwork(_local_user,
                                                   _local_cert_data);
      // Registers the identity provider
      _providers.Add("GoogleBackend", google_backend);
      // Register the social network
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
      ProtocolLog.WriteIf(SocialLog.SVPNLog, "GET FRIENDS: " +
                          DateTime.Now.TimeOfDay);

      List<string> friends = new List<string>();
      foreach(ISocialNetwork network in _networks.Values) {
        List<string> tmp_friends = network.GetFriends();
        if(tmp_friends == null) {
          continue;
        }
        foreach(string friend in tmp_friends) {
          if(friend.Length > 5 || !friends.Contains(friend)) {
            friends.Add(friend);
          }
        }
      }

      // Add friends from manual input
      foreach(string friend in _friends) {
        if(!friends.Contains(friend)) {
          friends.Add(friend);
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
      ProtocolLog.WriteIf(SocialLog.SVPNLog, "GET FINGERPRINTS: " +
                          DateTime.Now.TimeOfDay);

      List<string> fingerprints = new List<string>();
      foreach(IProvider provider in _providers.Values) {
        List<string> tmp_fprs = provider.GetFingerprints(uids);
        if(tmp_fprs == null) {
          continue;
        }
        foreach(string fpr in tmp_fprs) {
          if(fpr.Length > 50 || !fingerprints.Contains(fpr)) {
            fingerprints.Add(fpr);
          }
        }
      }
      // Add fingerprints from manual input
      foreach(string fpr in _fingerprints) {
        if(!fingerprints.Contains(fpr)) {
          fingerprints.Add(fpr);
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
      ProtocolLog.WriteIf(SocialLog.SVPNLog, "GET CERTIFICATES: " +
                          DateTime.Now.TimeOfDay);

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
      // Add certificates from manual input
      foreach(byte[] cert in _certificates) {
        if(!certificates.Contains(cert)) {
          certificates.Add(cert);
        }
      }
      return certificates;
    }

    /**
     * Stores the fingerprint of local user.
     * @return boolean indicating success.
     */
    public bool StoreFingerprint() {
      ProtocolLog.WriteIf(SocialLog.SVPNLog, "STORE FINGERPRINT: " +
                          DateTime.Now.TimeOfDay + " " +
                          _local_user.DhtKey + " " + _local_user.Address);

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

    /**
     * Loads certificates from the file system.
     */
    protected void LoadCertificates() {
      string[] cert_files = null;
      try {
        cert_files = System.IO.Directory.GetFiles(_cert_dir);
      } catch (Exception e) {
        ProtocolLog.WriteIf(SocialLog.SVPNLog, e.Message);
        ProtocolLog.WriteIf(SocialLog.SVPNLog, "LOAD CERTIFICATES FAILURE");
      }
      foreach(string cert_file in cert_files) {
        byte[] cert_data = SocialUtils.ReadFileBytes(cert_file);
        _certificates.Add(cert_data);
      }
    }

    /**
     * Adds a list of fingerprints seperated by newline.
     * @param fprlist a list of fingerprints.
     */
    public void AddFingerprints(string fprlist) {
      string[] fprs = fprlist.Split('\n');
      foreach(string fpr in fprs) {
        if(!_fingerprints.Contains(fpr)) {
          _fingerprints.Add(fpr);
        }
      }
    }

    /**
     * Adds a certificate to the socialvpn system.
     * @param certString a base64 encoding string representing certificate.
     */
    public void AddCertificate(string certString) {
      certString = certString.Replace("\n", "");
      byte[] certData = Convert.FromBase64String(certString);
      if(!_certificates.Contains(certData)) {
        _certificates.Add(certData);
      }
    }

    /**
     * Adds a list of friends seperated by newline.
     * @param friendlist a list of friends unique identifiers.
     */
    public void AddFriends(string friendlist) {
      string[] friends = friendlist.Split('\n');
      foreach(string friend in friends) {
        if(!_friends.Contains(friend)) {
          _friends.Add(friend);
        }
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
