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

  public class JabberNetwork {

    public const string SVPNKEYNS = "jabber:iq:svpnkey";

    public const string SVPNRESOURCE = "SVPN_XMPP";

    public event EventHandler ProcessEvent;

    protected readonly JabberClient _jclient;

    protected StatusTypes _status;

    public JabberNetwork(string network_host, string jabber_port) {

      _status = StatusTypes.Offline;
      _jclient = new JabberClient();
      _jclient.Port = Int32.Parse(jabber_port);
      _jclient.NetworkHost = network_host;
      _jclient.AutoReconnect = 30F;
      _jclient.AutoStartTLS = true;
      _jclient.KeepAlive = 30F;
      _jclient.AutoPresence = false;
      _jclient.AutoRoster = false;
      _jclient.LocalCertificate = null;
      Random rand = new Random();
      _jclient.Resource = SVPNRESOURCE + rand.Next(Int32.MaxValue);

      _jclient.OnAuthenticate += HandleOnAuthenticate;
      _jclient.OnPresence += HandleOnPresence;
      _jclient.OnIQ += HandleOnIQ;
      _jclient.OnInvalidCertificate += 
        new RemoteCertificateValidationCallback(HandleInvalidCert);
      
      _jclient.OnError += HandleOnError;
      _jclient.OnAuthError += HandleOnAuthError;
      _jclient.OnReadText += HandleOnReadText;
      _jclient.OnWriteText += HandleOnWriteText;
    }
    
    protected void HandleOnAuthError(object sender, System.Xml.XmlElement rp) {
      UpdateStatus(StatusTypes.Failed);
      Console.WriteLine(rp.OuterXml);
    }

    protected void HandleOnError(object sender, Exception ex){
      Console.WriteLine(ex.ToString());
    }

    protected void HandleOnAuthenticate(object sender) {
      UpdateStatus(StatusTypes.Online);
      Presence pres = new Presence(_jclient.Document);
      pres.Show = "dnd";
      pres.Status = "Chat Disabled";
      _jclient.Write(pres);
    }

    protected bool HandleInvalidCert(object sender, X509Certificate cert, 
      X509Chain chain, SslPolicyErrors errors) {
      string cert_info = String.Format("\nXMPP Server Data\n" + 
                         "Subject: {0}\nIssuer: {1}\n" + 
                         "SHA1 Fingerprint: {2}\n", cert.Subject, cert.Issuer,
                         cert.GetCertHashString());
      byte[] cert_data = Encoding.UTF8.GetBytes(cert_info);
      string path = _jclient.Server + "-server-cert-data.txt";
      SocialUtils.WriteToFile(cert_data, path);
      return true;
    }

    protected void HandleOnReadText(object sender, string txt) {
      //Console.WriteLine("RECV: " + txt);
    }

    protected void HandleOnWriteText(object sender, string txt) {
      //Console.WriteLine("SENT: " + txt);
    }

    protected void HandleOnPresence(Object sender, Presence pres) {
      if(pres.From.Resource != null && 
         pres.From != _jclient.JID &&
         pres.From.Resource.StartsWith(SVPNRESOURCE)) {
        IQ iq = new IQ(_jclient.Document);
        iq.To = pres.From;
        iq.From = _jclient.JID;
        iq.Query = _jclient.Document.CreateElement(null, "query", SVPNKEYNS);
        _jclient.Write(iq);
      }
    }

    protected void HandleOnIQ(Object sender, IQ iq) {
      if(iq.Query != null && iq.Query.NamespaceURI != null && 
         iq.Query.NamespaceURI == SVPNKEYNS) {
        if(iq.Type == IQType.get) {
          iq = iq.GetResponse(_jclient.Document);
          iq.Query.SetAttribute("value", GetQueryResponse());
          _jclient.Write(iq);
        }
        else if (iq.Type == IQType.result) {
          string uid = iq.From.User + "@" + iq.From.Server;
          ProcessResponse(iq.Query.GetAttribute("value"), uid);
        }
      }
    }

    protected string UpdateStatus(StatusTypes status) {
      _status = status;
      Dictionary<string, string> request = new Dictionary<string,string>();
      request["m"] = "updatestat";
      request["status"] = status.ToString();
      return FireEvent(request);
    }

    protected string GetQueryResponse() {
      Dictionary<string, string> request = new Dictionary<string,string>();
      request["m"] = "getcert";
      return FireEvent(request);
    }

    protected string ProcessResponse(string cert, string uid) {
      Dictionary<string, string> request = new Dictionary<string,string>();
      request["m"] = "add";
      request["cert"] = cert;
      request["uid"] = uid;
      return FireEvent(request);
    }

    protected string FireEvent(Dictionary<string,string> request) {
      EventHandler process_event = ProcessEvent;
      string response = String.Empty;
      if (process_event != null) {
        try {
          process_event(request, EventArgs.Empty);
          response = request["response"];
        } catch (Exception e) {
          response = e.Message;
          Console.WriteLine(response);
        }
      } 
      return response;
    }

    public void Login(string username, string password) {
      if(_status != StatusTypes.Online) {
        JID jid = new JID(username);
        _jclient.User = jid.User;
        _jclient.Server = jid.Server;
        _jclient.Password = password;
        _jclient.Connect();
      }
    }

    public void Logout() {
      if(_status == StatusTypes.Online) {
        _jclient.Close();
        UpdateStatus(StatusTypes.Offline);
      }
    }

    public void ProcessHandler(Object obj, EventArgs eargs) {
      Dictionary <string, string> request = (Dictionary<string, string>)obj;
      string method = String.Empty;
      if (request.ContainsKey("m")) {
        method = request["m"];
      }

      switch(method) {
        case "jabber.login":
          Login(request["uid"], request["pass"]);
          break;

        case "jabber.logout":
          Logout();
          break;

        default:
          break;
      }
    }

  }

#if SVPN_NUNIT
  [TestFixture]
  public class JabberNetworkTester {
    [Test]
    public void JabberNetworkTest() {
      JabberNetwork jabber = new JabberNetwork(null, "5222");
      Console.Write("Please enter jabber id and password: ");
      string input = Console.ReadLine();
      string[] parts = input.Split(' ');
      jabber.Login(parts[0], parts[1]);
      Console.ReadLine();
      jabber.Logout();
    }
  } 
#endif
}
