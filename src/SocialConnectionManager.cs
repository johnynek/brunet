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
using System.Threading;
using System.IO;

using Brunet;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  public class QueueItem {

    public enum Actions {
      AddCert,
      Sync,
      Process,
      HeartBeat
    }

    public Actions Action;
    public object Obj;

    public QueueItem(Actions action, object obj) {
      this.Action = action;
      this.Obj = obj;
    }
  }

  /**
   * This class is in charge of making connections between friends. It
   * manages the social networking backends as well as the identity providers
   * of the system.
   */
  public class SocialConnectionManager {

    /**
     * The node which accepts peers based on certificates.
     */
    protected readonly SocialNode _snode;

    /**
     * The social network and identity provider.
     */
    protected readonly SocialNetworkProvider _snp;

    /** 
     * The HTTP interface to manage socialvpn.
     */
    protected readonly HttpInterface _http;

    /**
     * The handles RPC for socialvpn.
     */
    protected readonly SocialRpcHandler _srh;

    /**
     * The main blocking queue used for message passing between threads.
     */
    protected readonly BlockingQueue _queue;

    /**
     * Main processing thread thread.
     */
    protected readonly Thread _main_thread;

    /**
     * The time keepers.
     */
    protected DateTime _last_update, _last_publish, _last_store, _last_ping;

    /**
     * Interval for periodic operations
     */
    protected readonly TimeSpan _interval;

    /**
     * Constructor.
     * @param node the social node.
     * @param provider the identity provider.
     * @param network the social network.
     * @param port the port number for the HTTP interface.
     * @param srh the social rpc handler.
     */
    public SocialConnectionManager(SocialNode node,SocialNetworkProvider snp,
                                   SocialRpcHandler srh, string port,
                                   BlockingQueue queue) {
      _snode = node;
      _snp = snp;
      _http = new HttpInterface(port);
      _http.ProcessEvent += ProcessHandler;
      _http.Start();
      _srh = srh;
      _queue = queue;
      _main_thread = new Thread(Start);
      _main_thread.Start();
      _interval = new TimeSpan(0,5,0);
      _last_update = DateTime.Now - _interval;
      _last_store = _last_update;
      _last_publish = _last_update;
      _last_ping = _last_update;
    }

    public void Start() {
      if (Thread.CurrentThread.Name == null) {
        Thread.CurrentThread.Name = "svpn_main_thread";
      }

      while(true) {
        QueueItem item = (QueueItem)_queue.Dequeue();
        switch(item.Action) {
          case QueueItem.Actions.AddCert:
            _snode.AddCertificate((byte[]) item.Obj);      
            break;

          case QueueItem.Actions.Sync:
            UpdateFriends((string) item.Obj);
            break;

          case QueueItem.Actions.Process:
            ProcessRequest((Dictionary<string, string>) item.Obj);
            break;

          case QueueItem.Actions.HeartBeat:
            HeartBeatAction();
            break;

          default:
            break;
        }
      }
    }

    /**
     * Heartbeat event handler.
     * @param obj the default object.
     * @param eargs the event arguments.
     */
    public void HeartBeatHandler(Object obj, EventArgs eargs) {
      _queue.Enqueue(new QueueItem(QueueItem.Actions.HeartBeat, obj));
    }

    /**
     * Timer event handler.
     * @param obj the default object.
     */
    public void HeartBeatAction(){
      if(!_snode.Dht.Activated) {
        return;
      }
      DateTime now = DateTime.Now;
      try {
        if((now - _last_update).Minutes >= 5 ) {
          UpdateFriends(null);
          _last_update = now;
        }
        if((now - _last_store).Minutes >= 5 ) {
          _snp.StoreFingerprint();
          _last_store = now;
        }
        if((now - _last_publish).Minutes >= 5 ) {
          _snode.PublishCertificate();
          _last_publish = now;
        }
        if((now - _last_ping).Minutes >= 5 ) {
          _srh.PingFriends();
          _last_ping = now;
        }
      } catch (Exception e) {
        ProtocolLog.WriteIf(SocialLog.SVPNLog, e.Message);
        ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                            String.Format("HEARTBEAT ACTION FAILURE: {0}",
                            DateTime.Now.TimeOfDay));
      }
    }

    /**
     * Process event handler.
     * @param obj the default object.
     * @param eargs the event arguments.
     */
    public void ProcessHandler(Object obj, EventArgs eargs) {
      ((Dictionary<string, string>)obj)["response"] = _snode.GetState();
      // Allows main thread to run request, main thread does all the work
      _queue.Enqueue(new QueueItem(QueueItem.Actions.Process, obj));
    }

    /**
     * Process event handler.
     * This is run by the main_thread.
     * @param request the dictionary containing request params
     */
    public void ProcessRequest(Dictionary<string, string> request) {
      if(request.ContainsKey("m")) {
        switch(request["m"]) {
          case "add":
            _snp.AddFriends(request["uids"]);
            break;

          case "addfpr":
            _snp.AddFingerprints(request["fprs"]);
            break;

          case "addcert":
            _snp.AddCertificate(request["cert"]);
            break;

          case "login":
            _snp.Login(request["id"], request["user"], request["pass"]);
            break;
            
          case "allow":
            AllowFriends(request["fprs"]);
            break;

          case "block":
            BlockFriends(request["fprs"]);
            break;

          default:
            break;
        }
        if(request["m"] == "add" || request["m"] == "addfpr" || 
            request["m"] == "addcert") {
          UpdateFriends(null);
        }
      }
    }

    /**
     * Updates friends and adds to socialvpn.
     * @param uid given if only one friends needs updating
     */
    protected void UpdateFriends(string uid) {
      if(uid != null && _snp.GetFriends().Contains(uid)) {
        AddFriends(new string[] {uid});
      }
      else {
        AddFriends(_snp.GetFriends().ToArray());
      }
    }

    /**
     * Allow a list of fingerprints seperated by newline.
     * @param fprlist a list of fingerprints.
     */
    protected void AllowFriends(string fprlist) {
      string[] fprs = fprlist.Split('\n');
      foreach(string fpr in fprs) {
        _snode.AddFriend(fpr);
      }
    }

    /**
     * Block a list of fingerprints seperated by newline.
     * @param fprlist a list of fingerprints.
     */
    protected void BlockFriends(string fprlist) {
      string[] fprs = fprlist.Split('\n');
      foreach(string fpr in fprs) {
        _snode.RemoveFriend(fpr);
      }
    }

    /**
     * Adds a list of friend based on user id.
     * @param uids the list of friend's user id.
     */
    protected void AddFriends(string[] uids) {
      List<byte[]> certificates = _snp.GetCertificates(uids);
      foreach(byte[] cert in certificates) {
        _snode.AddCertificate(cert);
      }
      List<string> fingerprints = _snp.GetFingerprints(uids);
      foreach(string fpr in fingerprints) {
        _snode.AddDhtFriend(fpr);
      }
    }
  }

#if SVPN_NUNIT
  [TestFixture]
  public class SocialConnectionManagerTester {
    [Test]
    public void SocialConnectionManagerTest() {
      Assert.AreEqual("test", "test");
    }
  } 
#endif
}
