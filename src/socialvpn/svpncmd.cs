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

using Brunet;
using Brunet.Applications;

namespace SocialVPN {

  /**
   * SocialNode Class. Extends the RpcIpopNode to support adding friends based
   * on X509 certificates.
   */
  public class Svpncmd {

    public static string _url = null;

    public static void SetUrl() {
      if(_url == null && System.IO.File.Exists("social.config")) {
        SocialConfig config = Utils.ReadConfig<SocialConfig>("social.config");
        _url = "http://127.0.0.1:" + config.HttpPort + "/api";
      }
    }

    public static bool CreateCertificate(string uid, string pcid, string name) {
      string config_path = "brunet.config";
      NodeConfig node_config = Utils.ReadConfig<NodeConfig>(config_path);
      if(!System.IO.Directory.Exists(node_config.Security.CertificatePath)) {
        string version = SocialNode.VERSION;
        string country = SocialNode.COUNTRY;
        

        node_config.NodeAddress = Utils.GenerateAHAddress().ToString();
        Utils.WriteConfig(config_path, node_config);
        SocialUtils.CreateCertificate(uid, name, pcid, version, country,
                                      node_config.NodeAddress, 
                                      node_config.Security.CertificatePath,
                                      node_config.Security.KeyPath);

        if(!System.IO.File.Exists("social.config")) {
          CreateConfig();
        }

        return true;
      }
      return false;
    }

    public static void CreateConfig() {
      SocialConfig social_config = new SocialConfig();
      social_config.BrunetConfig = "brunet.config";
      social_config.IpopConfig = "ipop.config";
      social_config.HttpPort = "58888";
      social_config.JabberPort = "5222";
      social_config.GlobalAccess = "off";
      Utils.WriteConfig("social.config", social_config);
    }

    public static void MakeCall(string method, string fprs) {
      Dictionary<string, string> parameters = 
        new Dictionary<string, string>();

      parameters["m"] = method;
      if(method == "add") {
        parameters["uids"] = fprs;
      }
      else {
        parameters["fprs"] = fprs;
      }
      try {
        SocialUtils.Request(_url, parameters);
      } catch(Exception e) {
        Console.WriteLine(e.Message);
        Console.WriteLine("Could not connect to SocialVPN, make sure" +
                          "process is running");
      }
      System.Threading.Thread.Sleep(2000);
      GetState();
    }

    public static void Login(string user, string pass) {
      Dictionary<string, string> parameters = 
        new Dictionary<string, string>();

      parameters["m"] = "login";
      parameters["id"] = "jabber";
      parameters["user"] = user;
      parameters["pass"] = pass;

      try {
        SocialUtils.Request(_url, parameters);
      } catch(Exception e) {
        Console.WriteLine(e.Message);
        Console.WriteLine("Could not connect to SocialVPN, make sure" +
                          "process is running");
      }
      System.Threading.Thread.Sleep(2000);
      GetState();
    }

    public static void Logout() {
      Dictionary<string, string> parameters = 
        new Dictionary<string, string>();

      parameters["m"] = "logout";

      try {
        SocialUtils.Request(_url, parameters);
      } catch(Exception e) {
        Console.WriteLine(e.Message);
        Console.WriteLine("Could not connect to SocialVPN, make sure" +
                          "process is running");
      }
      System.Threading.Thread.Sleep(2000);
      GetState();
    }

    public static void GetState() {
      string stateString = SocialUtils.Request(_url);
      PrintInfo(stateString);
      PrintFriends(stateString);
    }

    public static void PrintInfo(string stateString) {
      SocialState state = SocialUtils.XmlToObject<SocialState>(stateString);
      Console.WriteLine("Name: {0}\nAlias: {1}\nIP: {2}\nStatus: {3}\n" +
                        "Fingerprint: {4}\n",
                         state.LocalUser.Name, state.LocalUser.Alias,
                         state.LocalUser.IP, state.Status, state.LocalUser.DhtKey);
    }

    public static void PrintFriends(string stateString) {
      SocialState state = SocialUtils.XmlToObject<SocialState>(stateString);
      Console.WriteLine("{0},{1},{2},{3},{4}",
                          "Name", "Alias", "IP", "Status", 
                          "Fingerprint");
      foreach(SocialUser friend in state.Friends) {
        string status = "Online";
        if(friend.Time == "0" & friend.Access == "Allow") {
          status = "Offline";
        }
        else if(friend.Access == "Block") {
          status = "Blocked";
        }
        Console.WriteLine("{0},{1},{2},{3},{4}",
                          friend.Name, friend.Alias, friend.IP, 
                          status, friend.DhtKey);
      }
    }

    public static void ShowHelp() {
      string help = "usage: svpncmd.exe <option> <fingerprint>\n\n" +
                    "options:\n" +
                    "  info - shows current user's info and friends\n" +
                    "  login <user> <pass> - log in user\n" +
                    "  logout - log out user\n" +
                    "  add email fpr - add a friend by fingerprint\n" +
                    "  unblock fpr - unblock a friend's pc by fingerprint\n" +
                    "  block fpr - block a friend's by fingerprint\n" + 
                    "  help - shows this help";
      Console.WriteLine(help);
    }

    /**
     * The main function, starting point for the program.
     */
    public static void Main(string[] args) {
      SetUrl();
      if(args.Length == 0) {
        ShowHelp();
      }
      else if(args[0] == "help") {
        ShowHelp();
      }
      else if(args[0] == "info") {
        GetState();
      }
      else if(args[0] == "cert") {
        CreateCertificate(args[1], args[2], args[3]);
      }
      else if(args[0] == "login") {
        Login(args[1], args[2]);
      }
      else if(args[0] == "logout") {
        Logout();
      }
      else if(args[0] == "add") {
        MakeCall(args[0], args[1] + " " + args[2]);
      }
      else if(args[0] == "unblock") {
        MakeCall("allow", args[1]);
      }
      else if(args[0] == "block") {
        MakeCall("block", args[1]);
      }
      else {
        ShowHelp();
      }
    }
  }
}
