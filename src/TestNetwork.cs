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
using System.Collections;
using System.Collections.Generic;
using CookComputing.XmlRpc;

using Brunet;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  public class TestNetwork : IProvider, ISocialNetwork {

    protected readonly string _url;

    protected readonly SocialUser _local_user;

    public TestNetwork(SocialUser user) {
      _local_user = user;
      _url = "http://socialvpntest.appspot.com/api/?";
    }

    public bool Login(string username, string password) {
      return true;
    }

    public bool Logout() {
      return true;
    }

    public List<string> GetFriends() {
      List<string> new_friends = new List<string>();
      string vars = "m=getfriends";
      string response = SocialUtils.HttpRequest(_url + vars);
      string[] friends = response.Split('\n');
      foreach(string friend in friends) {
        Console.WriteLine(friend);
      }
      return new_friends;
    }

    public List<string> GetFingerprints(string uid) {
      List<string> fingerprints = new List<string>();
      string vars = "m=getfprs";
      string response = SocialUtils.HttpRequest(_url + vars);
      string[] fprs = response.Split('\n');
      foreach(string fpr in fprs) {
        Console.WriteLine(fpr);
      }
      return fingerprints;
    }

    public bool StoreFingerprint() {
      string vars = "m=store&uid=" + _local_user.Uid + "&fpr=" + 
        _local_user.DhtKey;
      SocialUtils.HttpRequest(_url + vars);
      return true;
    }

    public bool ValidateCertificate(Certificate cert) {
      return true;
    }
  }

#if SVPN_NUNIT
  [TestFixture]
  public class TestNetworkTester {
    [Test]
    public void TestNetworkTest() {
      string uid = "ptony82@ufl.edu";
      string name = "Pierre St Juste";
      string pcid = "pdesktop";
      string version = "SVPN_0.3.0";
      string country = "US";

      SocialUtils.CreateCertificate(uid, name, pcid, version, country,
                                    "address1234", "certificates", 
                                    "private_key");


      string cert_path = System.IO.Path.Combine("certificates", "lc.cert");
      byte[] cert_data = SocialUtils.ReadFileBytes(cert_path);
      SocialUser user = new SocialUser(cert_data);

      TestNetwork backend = new TestNetwork(user);
      backend.StoreFingerprint();
      backend.GetFriends();
      backend.GetFingerprints("uid");
    }
  } 
#endif

}
