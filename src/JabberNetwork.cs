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
using System.Text;

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Brunet;
using Brunet.Security;
using Brunet.DistributedServices;

using jabber;
using jabber.client;
using jabber.protocol.client;
using jabber.protocol.iq;
using jabber.connection;
using bedrock.util;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  public class JabberNetwork : ISocialNetwork, IProvider {

    public const string SVPNKEY = "svpnkey";

    protected readonly JabberClient _jclient;

    protected readonly SocialUser _local_user;

    protected readonly Dictionary<string, List<string>> _friends;

    protected readonly BlockingQueue _queue;

    protected bool _online;

    protected bool _auth_pending;

    public JabberNetwork(SocialUser user, byte[] certData, 
      BlockingQueue queue) {
      _local_user = user;
      _queue = queue;
      _friends = new Dictionary<string, List<string>>();
      _online = false;
      _auth_pending = false;
      _jclient = new JabberClient();

      _jclient.AutoReconnect = 30F;
      _jclient.AutoStartTLS = true;
      _jclient.KeepAlive = 30F;
      _jclient.AutoPresence = false;
      _jclient.AutoRoster = false;
      _jclient.LocalCertificate = null;
      
      _jclient.OnError += new bedrock.ExceptionHandler(OnError);
      _jclient.OnAuthError += new jabber.protocol.ProtocolHandler(OnAuthError);

      //_jclient.OnReadText += new bedrock.TextHandler(OnReadText);
      //_jclient.OnWriteText += new bedrock.TextHandler(OnWriteText);
      _jclient.OnPresence += new PresenceHandler(OnPresence);
      _jclient.OnAuthenticate += new bedrock.ObjectHandler(OnAuthenticate);
      _jclient.OnInvalidCertificate += 
        new RemoteCertificateValidationCallback(InvalidCertHandler);
    }
    
    private void OnAuthError(object sender, System.Xml.XmlElement rp) {
      _online = false;
      _auth_pending = false;
      Console.WriteLine("AUTH ERROR");
      Console.WriteLine(rp.OuterXml);
    }

    private void OnError(object sender, Exception ex){
      Console.WriteLine(ex.ToString());
    }

    private void OnAuthenticate(object sender) {
      string status = _local_user.DhtKey;
      string show = SVPNKEY;
      int priority = 24;
      _auth_pending = false;
      // Send presence message containing dhtkey
      _jclient.Presence(PresenceType.available, status, show, priority);
    }

    private bool InvalidCertHandler(object sender, X509Certificate cert, 
      X509Chain chain, SslPolicyErrors errors) {
      Console.WriteLine("We are blindly trusting this certificate");
      Console.WriteLine("Cert Hash String: {0}", cert.GetCertHashString());
      return true;
    }

    /*
    // only used for debugging
    private void OnReadText(object sender, string txt) {
      Console.WriteLine("RECV: " + txt);
    }

    // only used for debugging
    private void OnWriteText(object sender, string txt) {
      Console.WriteLine("SENT: " + txt);
    }
    */
    
    private void OnPresence(object sender, Presence pres) {
      if(pres.Show == SVPNKEY ) {
        string uid = pres.From.User + "@" + pres.From.Server;
        string fpr = pres.Status;
        if(!_friends.ContainsKey(uid)) {
          _friends[uid] = new List<string>();
          _friends[uid].Add(fpr);
          _queue.Enqueue(new QueueItem(QueueItem.Actions.Sync, uid));
          Console.WriteLine("Adding {0} {1}", uid, fpr);
        }
        else {
          if(!_friends[uid].Contains(fpr)) {
            _friends[uid].Add(fpr);
            _queue.Enqueue(new QueueItem(QueueItem.Actions.Sync, uid));
            Console.WriteLine("Adding {0} {1}", uid, fpr);
          }
        }
      }
    }

    public bool Login(string id, string username, string password) {
      if(_local_user.Uid != username) {
        throw new Exception("Jabber ID mismatch, cert uid does not match jabber id");
      }
      if(!_online) {
        JID jid = new JID(username);
        _jclient.User = jid.User;
        _jclient.Server = jid.Server;
        _jclient.Password = password;
        _jclient.Connect();
        _online = true;
        _auth_pending = true;

        while (_auth_pending) {
          // Wait for login to complete
          System.Threading.Thread.Sleep(5000);
        }
      }
      return _online;
    }

    public bool Logout() {
      if(_online) {
        _jclient.Close();
      }
      return true;
    }

    public List<string> GetFriends() {
      List<string> friends = new List<string>();
      foreach(string friend in _friends.Keys) {
        friends.Add(friend);
      }
      return friends;
    }

    public List<string> GetFingerprints(string[] uids) {
      List<string> fingerprints = new List<string>();
      foreach(string uid in uids) {
        if(_friends.ContainsKey(uid)) {
          foreach(string fpr in _friends[uid]) {
            fingerprints.Add(fpr);
          }
        }
      }
      return fingerprints;
    }

    public List<byte[]> GetCertificates(string[] uids) {
      return null;
    }

    public bool StoreFingerprint() {
      if(_jclient.IsAuthenticated) {
        // This calls sends presence message to all friends
        OnAuthenticate(null);
      }
      return true;
    }

    public bool ValidateCertificate(SocialUser user, byte[] certData) {
      if(_friends.ContainsKey(user.Uid) && 
         _friends[user.Uid].Contains(user.DhtKey)) {
        return true;
      }
      return false;
    }
  }

#if SVPN_NUNIT
  [TestFixture]
  public class JabberNetworkTester {
    [Test]
    public void JabberNetworkTest() {
      Certificate cert = SocialUtils.CreateCertificate("ptony82@gmail.com",
        "Pierre St Juste", "testpc", "version", "country", "address", 
        "certdir", "path");
      SocialUser user = new SocialUser(cert.X509.RawData);
      JabberNetwork jnetwork = new JabberNetwork(user, cert.X509.RawData);
      jnetwork.Login("jabber", "ptony82@gmail.com", "ob681021");
      Console.WriteLine("Waiting 15 seconds for resuls");
      System.Threading.Thread.Sleep(15000);
      Console.WriteLine("Done waiting for results");
      foreach(string friend in jnetwork.GetFriends()) Console.WriteLine(friend);
      jnetwork.GetFingerprints(new string[] {"ptony82@gmail.com"});
      jnetwork.StoreFingerprint();
      jnetwork.Logout();
    }
  } 
#endif

}
