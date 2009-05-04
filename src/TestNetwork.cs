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
    [XmlRpcUrl("http://localhost:8888")]
    public interface IPythonXmlRpc : IXmlRpcProxy {
      [XmlRpcMethod("StoreFingerprint")]
      string StoreFingerprint(string uid, string fingerprint);

      [XmlRpcMethod("GetFingerprints")]
      string[] GetFingerprints(string uid);

      [XmlRpcMethod("GetFriends")]
      string[] GetFriends(string uid);

      [XmlRpcMethod("SayHello")]
      string SayHello(string name);

    }

    protected readonly IPythonXmlRpc _backend;

    protected readonly SocialUser _local_user;

    public TestNetwork(SocialUser user) {
      _backend = XmlRpcProxyGen.Create<IPythonXmlRpc>();
      _local_user = user;
    }

    public bool Login(string username, string password) {
      return true;
    }

    public bool Logout() {
      return true;
    }

    public List<string> GetFriends() {
      List<string> new_friends = new List<string>();
      return new_friends;
    }

    public List<string> GetFingerprints(string uid) {
      List<string> fingerprints = new List<string>();
      string[] fprs = _backend.GetFingerprints(uid);
      foreach(string fpr in fprs) {
        Console.WriteLine(fpr);
      }
      return fingerprints;
    }

    public bool StoreFingerprint() {
      return true;
    }

    public void SayHello() {
      Console.WriteLine(_backend.SayHello("Pierre"));
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
      backend.SayHello();
      backend.GetFingerprints("uid");
    }
  } 
#endif

}
