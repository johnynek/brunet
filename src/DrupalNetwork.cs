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

  public class DrupalNetwork : IProvider, ISocialNetwork {
    [XmlRpcUrl("http://apocuf.org/drupal/services/xmlrpc")]
    public interface IDrupalXmlRpc : IXmlRpcProxy {
      [XmlRpcMethod("node.get")]
      XmlRpcStruct NodeGet(string sessid, int nid, string[] fields);

      [XmlRpcMethod("node.save")]
      string NodeSave(string sessid, XmlRpcStruct node);

      [XmlRpcMethod("system.connect")]
      XmlRpcStruct SystemConnect();

      [XmlRpcMethod("user.login")]
      XmlRpcStruct UserLogin(string sessid, string username, string password);

      [XmlRpcMethod("user.logout")]
      bool UserLogout(string sessid);

      [XmlRpcMethod("views.get")]
      XmlRpcStruct[] ViewsGet(string sessid, string view_name, 
                              string[] fields, 
                              string[] args, int offset, int limit);
    }

    protected string _sessid;

    protected IDrupalXmlRpc _drupal;

    protected SocialUser _local_user;

    protected string _drupal_uid;

    protected Dictionary<string, string> _email_to_uid;

    protected bool _uid_mismatch;

    protected bool _key_found;

    public DrupalNetwork(SocialUser user) {
      _sessid = null;
      _drupal = XmlRpcProxyGen.Create<IDrupalXmlRpc>();
      _local_user = user;
      _drupal_uid = null;
      _email_to_uid = new Dictionary<string,string>();
      _uid_mismatch = false;
      _key_found = false;
    }

    internal void Print(XmlRpcStruct data) {
      foreach(DictionaryEntry de in data) {
        Console.WriteLine(de.Key + ": " + de.Value);
      }
    }

    public bool Login(string username, string password) {
      XmlRpcStruct login = _drupal.SystemConnect();
      string sessid = (string)(login["sessid"]);
      XmlRpcStruct login_response = _drupal.UserLogin(sessid, username, 
                                                      password);
      XmlRpcStruct user = (XmlRpcStruct)login_response["user"];

      string uid = (string)user["mail"];
      _drupal_uid = (string)user["uid"];
      if(_local_user.Uid != uid) {
        _uid_mismatch = true;
        ProtocolLog.Write(SocialLog.SVPNLog, "Uid mismatch: " + uid);
      }
      _sessid = (string)login_response["sessid"];
      return true;
    }

    public bool Logout() {
      return _drupal.UserLogout(_sessid);
    }

    public List<string> GetFriends() {
      List<string> new_friends = new List<string>();
      string[] fields = new string[] {};
      string[] args = new string[] {};

      XmlRpcStruct[] friends = _drupal.ViewsGet(_sessid, "users", fields,
                                                args, 0, 0);

      foreach(XmlRpcStruct friend in friends) {
        string email = (string) friend["users_mail"];
        string uid = (string) friend["uid"];
        new_friends.Add(email);
        if(!_email_to_uid.ContainsKey(email)) {
          _email_to_uid.Add(email, uid);
        }
      }
      new_friends.Add(_local_user.Uid);
      return new_friends;
    }

    public List<string> GetFingerprints(string email) {
      List<string> fingerprints = new List<string>();

      if(_email_to_uid.ContainsKey(email)) {
        string uid = _email_to_uid[email];
        string[] fields = new string[] {};
        string[] args = new string[] {uid};

        XmlRpcStruct[] fprs = _drupal.ViewsGet(_sessid, "fingerprints", 
                                               fields, args, 0, 0);

        foreach(XmlRpcStruct fpr in fprs) {
          string dht_key = (string)fpr["node_revisions_body"];
          if(dht_key == _local_user.DhtKey) {
            _key_found = true;
            continue;
          }
          fingerprints.Add(dht_key);
        }
      }
      return fingerprints;
    }

    public bool StoreFingerprint() {
      if(_uid_mismatch) throw new Exception("Uid mismatch");

      if(!_key_found) {
        XmlRpcStruct saveData = new XmlRpcStruct();
        saveData["title"] = _local_user.PCID;
        saveData["body"] = _local_user.DhtKey;
        saveData["type"] = "fingerprint";
        _drupal.NodeSave(_sessid, saveData);
      }
      return true;
    }
  }

#if SVPN_NUNIT
  [TestFixture]
  public class DrupalNetworkTester {
    [Test]
    public void DrupalNetworkTest() {
      string uid = "ptony82@ufl.edu";
      string name = "Pierre St Juste";
      string pcid = "pdesktop";
      string version = "SVPN_0.3.0";
      string country = "US";

      SocialUtils.CreateCertificate(uid, name, pcid, version, country);

      string cert_path = System.IO.Path.Combine("certificates", "lc.cert");
      byte[] cert_data = SocialUtils.ReadFileBytes(cert_path);
      SocialUser user = new SocialUser(cert_data);

      DrupalNetwork drupal = new DrupalNetwork(user);
      drupal.Login("pierre", "stjuste");

      List<string> friends = drupal.GetFriends();

      foreach(string friend in friends) {
        Console.WriteLine(friend);
        List<string> fprs = drupal.GetFingerprints(friend);
        foreach(string fpr in fprs) {
          Console.WriteLine(friend + " " + fpr);
        }
      }
      drupal.StoreFingerprint();
      drupal.Logout();
    }
  } 
#endif

}
