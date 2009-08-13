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

  /**
   * Definition of QueueItem for Blocking Queue.
   */ 
  public class QueueItem {

    public enum Actions {
      AddCertTrue,
      AddCertFalse,
      DhtAdd,
      Sync,
      Process,
      HeartBeat,
      Publish
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
     * The common delimiter for user input
     */
    public const char DELIM = ',';

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
     * Global Dht Access.
     */
    protected bool _global_access;

    /**
     * The counter for heartbeat event.
     */
    protected int _heartbeat_counter;

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
      _last_update = DateTime.MinValue;
      _last_store = _last_update;
      _last_publish = _last_update;
      _last_ping = _last_update;
      _heartbeat_counter = 0;
    }

    /**
     * Sets the gbobal access option for automatic creation links.
     */
    public bool GlobalAccess {
      set {
        _global_access = value;
      }
      get {
        return _global_access;
      }
    }

    /**
     * Method for main thread, listens on queue for job to do
     */
    protected void Start() {
      if (Thread.CurrentThread.Name == null) {
        Thread.CurrentThread.Name = "svpn_main_thread";
      }

      while(true) {
        try {
          QueueItem item = (QueueItem)_queue.Dequeue();
          switch(item.Action) {
            case QueueItem.Actions.AddCertTrue:
              _snode.AddCertificate((byte[]) item.Obj, true);      
              break;

            case QueueItem.Actions.AddCertFalse:
              _snode.AddCertificate((byte[]) item.Obj, false);      
              break;

            case QueueItem.Actions.DhtAdd:
              if(_global_access) {
                _snode.AddDhtFriend((string) item.Obj, _global_access);
              }
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

            case QueueItem.Actions.Publish:
              _snode.PublishCertificate();
              break;

            default:
              break;
          }
        } catch (Exception e) {
          ProtocolLog.WriteIf(SocialLog.SVPNLog, e.Message);
          ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                              String.Format("QUEUE ACTION FAILURE: {0}",
                              DateTime.Now.TimeOfDay));
        }
      }
    }

    /**
     * Calls when the node is shutting down, close cleanly.
     */
    public void Stop() {
      _snp.Logout();
      _http.Stop();
      _main_thread.Abort();
      // TODO:Take out this line
      //Environment.Exit(1);
    }

    /**
     * Heartbeat event handler.
     * @param obj the default object.
     * @param eargs the event arguments.
     */
    public void HeartBeatHandler(Object obj, EventArgs eargs) {
      // Check every 15 seconds since heartbeart occurs every 500 ms
      if( _heartbeat_counter % 30 == 0) {
        if(!_snode.CertPublished) {
          _queue.Enqueue(new QueueItem(QueueItem.Actions.Publish, null));
        }
        else {
          _queue.Enqueue(new QueueItem(QueueItem.Actions.HeartBeat, obj));
        }
      }
      _heartbeat_counter++;
    }

    /**
     * Timer event handler.
     * @param obj the default object.
     */
    protected void HeartBeatAction(){
      DateTime now = DateTime.Now;
      DateTime min = DateTime.MinValue;
      if(_last_update == min || (now - _last_update).Minutes >= 5 ) {
        UpdateFriends(null);
        _last_update = now;
      }
      if(_last_store == min || (now - _last_store).Minutes >= 30 ) {
        _snp.StoreFingerprint();
        _last_store = now;
      }
      if(_last_publish == min || (now - _last_publish).Minutes >= 30 ) {
        _snode.PublishCertificate();
        _last_publish = now;
      }
      if(_last_ping == min || (now - _last_ping).Minutes >= 1 ) {
        _srh.PingFriends();
        _last_ping = now;
      }
    }

    /**
     * Process event handler.
     * @param obj the default object.
     * @param eargs the event arguments.
     */
    public void ProcessHandler(Object obj, EventArgs eargs) {
      Dictionary <string, string> request = (Dictionary<string, string>)obj;
      if(request.ContainsKey("m") && request["m"] == "login") {
        _snp.Login(request["id"], request["user"], request["pass"]);
      }
      // Allows main thread to run request, main thread does all the work
      _queue.Enqueue(new QueueItem(QueueItem.Actions.Process, obj));
      request["response"] = _snode.GetState(false);
    }

    /**
     * Process event handler.
     * This is run by the main_thread.
     * @param request the dictionary containing request params
     */
    protected void ProcessRequest(Dictionary<string, string> request) {
      if(request.ContainsKey("m")) {
        string method = request["m"];
        switch(method) {
          case "add":
            _snp.AddFriends(request["uids"].Split(DELIM));
            break;

          case "addcert":
            _snp.AddCertificate(request["cert"]);
            break;

          case "allow":
            AllowFriends(request["fprs"]);
            break;

          case "block":
            BlockFriends(request["fprs"]);
            break;

          case "exit":
            Stop();
            Environment.Exit(1);
            break;

          default:
            break;
        }
        if(method == "add" || method == "addcert" || method == "refresh" ||
           method == "login") {
          UpdateFriends(null);
        }
      }
    }

    /**
     * Updates friends and adds to socialvpn.
     * @param uid given if only one friends needs updating
     */
    protected void UpdateFriends(string uid) {
      // only get fingerprints for one friend
      if(uid != null && _snp.GetFriends().Contains(uid)) {
        AddFriends(new string[] {uid});
      }
      // Get fingerprints for all friends
      else {
        AddFriends(_snp.GetFriends().ToArray());
      }
    }

    /**
     * Allow a list of fingerprints seperated by newline.
     * @param fprlist a list of fingerprints.
     */
    protected void AllowFriends(string fprlist) {
      string[] fprs = fprlist.Split(DELIM);
      foreach(string fpr in fprs) {
        _snode.AddFriend(fpr);
      }
    }

    /**
     * Block a list of fingerprints seperated by newline.
     * @param fprlist a list of fingerprints.
     */
    protected void BlockFriends(string fprlist) {
      string[] fprs = fprlist.Split(DELIM);
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
        _snode.AddCertificate(cert, true);
      }
      List<string> fingerprints = _snp.GetFingerprints(uids);
      foreach(string fpr in fingerprints) {
        _snode.AddDhtFriend(fpr, true);
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
