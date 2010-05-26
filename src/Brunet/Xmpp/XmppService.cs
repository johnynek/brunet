/*
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
  
The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using jabber;
using jabber.client;
using jabber.protocol;
using jabber.protocol.iq;
using jabber.protocol.client;

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Xml;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Brunet.Util;

namespace Brunet.Xmpp {
  /// <summary> A simplified class for interacting with Xmpp.</summary>
  public class XmppService {
    public static BooleanSwitch XmppLog = new BooleanSwitch("XmppLog", "XmppLog");
    protected JabberClient _jc;
    protected Dictionary<string, JID> _online;
    protected Dictionary<Type, QueryCallback> _demux;
    protected object _write_lock;

    /// <summary> Method to implement for listeners of various IQ Queries.</summary>
    public delegate void QueryCallback(Element msg, JID from);
    public const string RESOURCE_NS = "Brunet.";
    /// <summary> The Xmpp Client's JID.</summary>
    public JID JID { get { return _jc.JID; } }
    /// <summary> The Xmpp Server's JID.</summary>
    public JID Server { get { return _jc.Server; } }
    /// <summary> True once the client has successfully authenticatd with the Server.</summary>
    public bool IsAuthenticated { get { return _jc.IsAuthenticated; } }
    /// <summary>Called during the client init, all handlers should register prior
    /// to calling Connect.</summary>
    public event jabber.connection.StreamHandler OnStreamInit {
      add { _jc.OnStreamInit += value; }
      remove { _jc.OnStreamInit -= value; }
    }

    /// <summary>Called once the client has authenticated with the server.<summary>
    public event bedrock.ObjectHandler OnAuthenticate {
      add { _jc.OnAuthenticate += value; }
      remove { _jc.OnAuthenticate -= value; }
    }

    /// <summary>Initiate a Xmpp client handle.</summary>
    public XmppService(string username, string password, int port)
    {
      _write_lock = new object();
      _jc = new JabberClient();

      JID jid = new JID(username);
      _jc.User = jid.User;
      _jc.Server = jid.Server;
      _jc.Password = password;
      _jc.Port = port;

      _jc.AutoReconnect = 30F;
      _jc.AutoStartTLS = true;
      _jc.KeepAlive = 30F;
      _jc.AutoPresence = false;
      _jc.AutoRoster = false;
      _jc.LocalCertificate = null;
      var rng = new RNGCryptoServiceProvider();
      byte[] id = new byte[4];
      rng.GetBytes(id);
      _jc.Resource = RESOURCE_NS + BitConverter.ToString(id).Replace("-", "");

      _jc.OnInvalidCertificate += HandleInvalidCert;
      _jc.OnAuthenticate += HandleAuthenticate;
      _jc.OnAuthError += HandleAuthError;
      _jc.OnError += HandleError;
      _jc.OnIQ += HandleIQ;
      _jc.OnPresence += HandlePresence;
      _jc.OnMessage += HandleMessage;

      _online = new Dictionary<string, JID>();
      _demux = new Dictionary<Type, QueryCallback>();
    }

    /// <summary>Connect to the Xmpp Server.</summary>
    public void Connect()
    {
      _jc.Connect();
    }

    /// <summary>Listen to a specific type of Query through typeof(Element).</summary>
    public void Register(Type et, QueryCallback method)
    {
      lock(_demux) {
        if(!_demux.ContainsKey(et)) {
          _demux[et] = method;
        } else {
          _demux[et] += method;
        }
      }
    }

    /// <summary>Stop listening for specific types of Queries through typeof(Element).</summary>
    public void Unregister(Type et, QueryCallback method)
    {
      lock(_demux) {
        _demux[et] -= method;
      }
    }

    /// <summary>Called to deal with invalid certificates, necessary or we can't
    /// access most xmpp servers.</summary>
    protected bool HandleInvalidCert(object sender, X509Certificate cert,
        X509Chain chain, SslPolicyErrors errors)
    {
      return true;
    }

    /// <summary>We are not available for chatting, we're just online to take
    /// advantage of Xmpp!</summary>
    protected void HandleAuthenticate(object sender)
    {
      Presence presence = new Presence(new XmlDocument());
      presence.Show = "dnd";
      presence.Status = "Chat Disabled";
      Write(presence);

      // We are online!
      lock(_online) {
        _online[JID.ToString()] = JID;
      }
    }

    /// <summary>Crap, we were denied access, all we can do is notify the user.
    /// Maybe they can change the user parameters and restart the process.</summary>
    protected void HandleAuthError(object sender, XmlElement rp)
    {
      ProtocolLog.WriteIf(ProtocolLog.Exceptions, rp.ToString());
    }

    /// <summary>Received an error, if a message is a datagram, then this doesn't
    /// matter so much, if we were waiting for a response, this needs to be
    /// reported.</summary>
    protected void HandleError(object sender, Exception e)
    {
      ProtocolLog.WriteIf(ProtocolLog.Exceptions, e.ToString());
    }

    protected void HandleMessage(object sender, Message msg)
    {
      XmlDocument dom = new XmlDocument();
      dom.LoadXml(msg.Body);
      XmlElement el = dom.DocumentElement;
      HandleQuery(el as Element, msg.From);
      return ;
    }

    /// <summary>Incoming IQ, process it and pass it to the proper HandleQuery,
    /// if one exists.</summary>
    protected void HandleIQ(object sender, IQ iq)
    {
      if(iq.Query == null) {
        return;
      }
      ProtocolLog.WriteIf(XmppLog, "Incoming: " + iq.ToString());

      // If we don't set true, it will respond with an error
      iq.Handled = HandleQuery(iq.Query as Element, iq.From); 
    }

    protected bool HandleQuery(Element query, JID from)
    {
      QueryCallback method = null;
      lock(_demux) {
        if(!_demux.TryGetValue(query.GetType(), out method)) {
          return false;
        }
      }

      // Jabber.Net doesn't handle exceptions well
      try {
        method(query, from);
      } catch (Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, e.ToString());
      }
      return true;
    }

    /// <summary>Received a presence message, type=unavailable means the user
    /// has gone offline, everything else means the user is online.</summary>
    protected void HandlePresence(object sender, Presence pres)
    {
      ProtocolLog.WriteIf(XmppLog, "Incoming: " + pres.ToString());
      JID jid = pres.From;
      if(jid == null || jid.Resource == null) {
        return;
      }

      string key = jid.ToString();
      if(jid.Resource.StartsWith(RESOURCE_NS)) {
        if(pres.Type == PresenceType.unavailable) {
          lock(_online) {
            _online.Remove(key);
          }
        } else if(!_online.ContainsKey(key)) {
          lock(_online) {
            _online[jid.ToString()] = jid;
          }
        }
      }
    }

    protected void Write(XmlElement data)
    {
      ProtocolLog.WriteIf(XmppLog, "Outgoing: " + data.ToString());
      lock(_write_lock) {
        _jc.Write(data);
      }
    }

    protected void SendByMessage(Element data, JID to)
    {
      Message msg = new Message(new XmlDocument());
      msg.From = _jc.JID;
      msg.To = to;
      msg.Body = data.InnerXml;
      Write(msg);
    }

    protected void SendByIQ(Element data, JID to)
    {
      IQ iq = new IQ(new XmlDocument());
      iq.From = _jc.JID;
      iq.To = to;
      iq.Query = data;
      Write(iq);
    }

    /// <summary>Send a Query to a remote peer.</summary>
    public bool SendTo(Element msg, JID to)
    {
      if(!_online.ContainsKey(to.ToString())) {
        return false;
      }

      SendByIQ(msg, to);
      return true;
    }

    /// <summary>Send a message to all of our remote peers.</summary>
    public void SendBroadcast(Element msg)
    {
      List<JID> jids = new List<JID>(_online.Count);
      lock(_online) {
        foreach(JID jid in _online.Keys) {
          jids.Add(jid);
        }
      }

      foreach(JID jid in jids) {
        SendByIQ(msg, jid);
      }
    }

    public void SendRandomMulticast(Element msg)
    {
      List<JID> jids = new List<JID>();
      lock(_online) {
        foreach(JID jid in _online.Keys) {
          jids.Add(jid);
        }
      }

      Random rand = new Random();
      var sent = new Dictionary<int, bool>();
      while(sent.Count < 10 && sent.Count < jids.Count) {
        int idx = rand.Next(0, jids.Count);
        if(sent.ContainsKey(idx)) {
          continue;
        }
        sent[idx] = true;
        SendByIQ(msg, jids[idx]);
      }
    }

    /// <summary>Is the peer online to the best of our knowledge.</summary>
    public bool IsUserOnline(JID jid)
    {
      if(!_online.ContainsKey(jid.ToString())) {
        return jid.Equals(_jc.JID);
      }
      return true;
    }

    /// <summary>Query a remote peer and provide a callback for the response.</summary>
    public void BeginQuery(XmlElement query, JID to, jabber.connection.IqCB end_query)
    {
      IQ iq = new IQ(new XmlDocument());
      iq.Type = IQType.get;
      iq.To = to;
      iq.From = _jc.JID;
      iq.Query = query;
      _jc.Tracker.BeginIQ(iq, end_query, null);
    }

    public void BeginQueryGoogleStunServers(jabber.connection.IqCB end_query)
    {
      XmlElement ele = (new XmlDocument()).CreateElement(null, "query", "google:jingleinfo");
      BeginQuery(ele, JID.Bare, end_query);
    }

    /*
    public void BeginServerDiscovery()
    {
      DiscoInfoIQ diq = new DiscoInfoIQ(new XmlDocument());
      diq.Type = IQType.get;
      diq.To = _jc.Server;
      Console.WriteLine(diq);
      _jc.Tracker.BeginIQ(diq, EndServerDiscovery, null);
    }

    protected void EndServerDiscovery(object sender, IQ iq, object state)
    {
      if(iq.Type != IQType.result) {
        return;
      }

      DiscoInfo info = iq.Query as DiscoInfo;
      if(info == null) {
        return;
      }

      foreach(DiscoFeature df in info.GetFeatures()) {
        Console.WriteLine(df);
      }
    }

    public void PrintRoster()
    {
      lock(_online) {
        foreach(var member in _online.Keys) {
          Console.WriteLine(member);
        }
      }
    }

    public static void Main(string[] args)
    {
      string username = args[0];
      string password = args[1];
      XmppService xt = new XmppService(username, password, 5222);
      xt.OnStreamInit += XmppRelayFactory.HandleStreamInit;
      xt.Register(typeof(XmppRelay), HandleXmppRelay);
      xt.Connect();
      while(true) {
        string cmd = Console.ReadLine();
        if(cmd == "") {
          break;
        }
        switch(cmd) {
          case "list":
            xt.PrintRoster();
            break;
          case "msg":
            Console.Write("To: ");
            string to = Console.ReadLine().Trim();
            Console.WriteLine("Msg: ");
            string msg = Console.ReadLine().Trim();
            xt.SendTo(new XmppRelay(new XmlDocument(), System.Text.Encoding.UTF8.GetBytes(msg)), new JID(to));
            break;
          case "discovery":
            Console.WriteLine("Beginning discovery");
            xt.BeginServerDiscovery();
            break;
          case "query":
            Console.Write("To: ");
            string nto = Console.ReadLine().Trim();
            Console.WriteLine("Query: ");
            string query = Console.ReadLine().Trim();
            XmlElement ele = (new XmlDocument()).CreateElement(null, "query", query);
            xt.BeginQuery(ele, new JID(nto), EndQuery);
            break;
          case "server":
            Console.WriteLine(xt.Server);
            break;
          case "stun":
            xt.BeginQueryGoogleStunServers(EndQuery);
            break;
          default:
            Console.WriteLine("Unsupported message");
            break;
        }
        Console.WriteLine();
      }
    }

    public static void HandleXmppRelay(Element msg, JID from)
    {
      XmppRelay xr = msg as XmppRelay;
      if(xr == null) {
        return;
      }

      Console.WriteLine(from + ": " + System.Text.Encoding.UTF8.GetString(xr.Data));
    }

    public static void EndQuery(object sender, IQ iq, object state)
    {
      Console.WriteLine(iq);
    }
    */
  }
}
