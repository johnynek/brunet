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
using System.Xml;

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

    public const string SVPNKEYNS = "jabber:iq:svpnkey";

    public const string SVPNRESOURCE = "SVPN_XMPP";

    protected readonly JabberClient _jclient;

    protected readonly SocialUser _local_user;

    protected readonly Dictionary<string, List<string>> _friends;

    protected readonly BlockingQueue _queue;

    protected bool _online;

    protected bool _auth_pending;

    protected bool _pres_sent;

    public JabberNetwork(SocialUser user, byte[] certData, 
      BlockingQueue queue, string jabber_port) {
      _local_user = user;
      _queue = queue;
      _friends = new Dictionary<string, List<string>>();
      _online = false;
      _auth_pending = false;
      _pres_sent = false;
      _jclient = new JabberClient();

      _jclient.Port = Int32.Parse(jabber_port);
      _jclient.AutoReconnect = 30F;
      _jclient.AutoStartTLS = true;
      _jclient.KeepAlive = 30F;
      _jclient.AutoPresence = false;
      _jclient.AutoRoster = false;
      _jclient.LocalCertificate = null;
      _jclient.Resource = SVPNRESOURCE + 
        _local_user.Fingerprint.Substring(0, 10);
      
      _jclient.OnError += HandleOnError;
      _jclient.OnAuthError += HandleOnAuthError;

#if SVPN_NUNIT
      _jclient.OnReadText += HandleOnReadText;
      _jclient.OnWriteText += HandleOnWriteText;
#endif
      _jclient.OnAuthenticate += HandleOnAuthenticate;
      _jclient.OnPresence += HandleOnPresence;
      _jclient.OnIQ += HandleOnIQ;
      _jclient.OnInvalidCertificate += HandleInvalidCert;
    }
    
    private void HandleOnAuthError(object sender, System.Xml.XmlElement rp) {
      _online = false;
      _auth_pending = false;
      Console.WriteLine(rp.OuterXml);
    }

    private void HandleOnError(object sender, Exception ex){
      Console.WriteLine(ex.ToString());
    }

    private void HandleOnAuthenticate(object sender) {
      _auth_pending = false;
      Presence pres = new Presence(_jclient.Document);
      pres.Show = "dnd";
      pres.Status = "Chat Disabled";
      _jclient.Write(pres);
    }

    private bool HandleInvalidCert(object sender, X509Certificate cert, 
      X509Chain chain, SslPolicyErrors errors) {
      string cert_info = String.Format("\nXMPP Server Data\n" + 
                         "Subject: {0}\nIssuer: {1}\n" + 
                         "SHA1 Fingerprint: {2}\n", cert.Subject, cert.Issuer,
                         cert.GetCertHashString());
      byte[] cert_data = Encoding.UTF8.GetBytes(cert_info);
      string path = _jclient.Server + "-server-data.txt";
      SocialUtils.WriteToFile(cert_data, path);
      return true;
    }

#if SVPN_NUNIT
    // only used for debugging
    private void HandleOnReadText(object sender, string txt) {
      Console.WriteLine("RECV: " + txt);
    }

    // only used for debugging
    private void HandleOnWriteText(object sender, string txt) {
      Console.WriteLine("SENT: " + txt);
    }
#endif

    private void HandleOnPresence(Object sender, Presence pres) {
      if(pres.From.Resource != null && 
         pres.From != _jclient.JID &&
         pres.From.Resource.StartsWith(SVPNRESOURCE)) {
        IQ iq = new IQ(_jclient.Document);
        iq.To = pres.From;
        iq.From = _jclient.JID;
        iq.Query = _jclient.Document.CreateElement(null, "query", SVPNKEYNS);
        _jclient.Write(iq);
      }
      if(!_pres_sent && pres.From.Bare == _jclient.JID.Bare &&
         pres.From != _jclient.JID && pres.Type == PresenceType.available) {
        // Mirror another client that's available
        Presence new_pres = new Presence(_jclient.Document);
        new_pres.Show = pres.Show;
        new_pres.Status = pres.Status;
        _jclient.Write(new_pres);
        _pres_sent = true;
      }
    }

    private void HandleOnIQ(Object sender, IQ iq) {
      if(iq.Query != null && iq.Query.NamespaceURI != null && 
         iq.Query.NamespaceURI == SVPNKEYNS) {
        if(iq.Type == IQType.get) {
          iq = iq.GetResponse(_jclient.Document);
          iq.Query.SetAttribute("value", _local_user.DhtKey);
          _jclient.Write(iq);
        }
        else if (iq.Type == IQType.result) {
          string friend = iq.From.User + "@" + iq.From.Server;
          string fpr = iq.Query.GetAttribute("value");
          if(_friends.ContainsKey(friend)) {
            if(!_friends[friend].Contains(fpr)) {
              _friends[friend].Add(fpr);
            }
          }
          else {
            List<string> fprs = new List<string>();
            fprs.Add(fpr);
            _friends.Add(friend, fprs);
            Console.WriteLine("Friend {0} at {1}", friend, fpr);
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
        _online = false;
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
      string userid = "pierre@pdebian64";
      Certificate cert = SocialUtils.CreateCertificate(userid,
        "Pierre St Juste", "testpc", "version", "country", "address", 
        "certdir", "path");
      SocialUser user = new SocialUser(cert.X509.RawData);
      BlockingQueue queue = new BlockingQueue();
      JabberNetwork jnetwork = new JabberNetwork(user, cert.X509.RawData, 
          queue, "5222");
      jnetwork.Login("jabber", userid,"stjuste");
      Console.WriteLine("Waiting 5 seconds for resuls");
      System.Threading.Thread.Sleep(5000);
      Console.WriteLine("Done waiting for results");
      foreach(string friend in jnetwork.GetFriends()) Console.WriteLine(friend);
      jnetwork.GetFingerprints(new string[] {userid});
      jnetwork.StoreFingerprint();
      jnetwork.Logout();
    }
  } 
#endif

}
