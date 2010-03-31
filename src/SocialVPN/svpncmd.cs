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
        _url = "http://127.0.0.1:" + config.HttpPort + "/state.xml";
      }
    }

    public static void CreateConfig(string uid) {
      SocialConfig social_config = new SocialConfig();
      social_config.BrunetConfig = "brunet.config";
      social_config.IpopConfig = "ipop.config";
      social_config.HttpPort = "58888";
      social_config.JabberPort = "5222";
      social_config.JabberID = uid;
      social_config.JabberPass = "password";
      social_config.AutoLogin = false;
      Utils.WriteConfig("social.config", social_config);
    }

    public static void CreateCertificate(string uid, string pcid, string name) {
      CreateConfig(uid);
      string config_path = "brunet.config";
      NodeConfig node_config = Utils.ReadConfig<NodeConfig>(config_path);
      string version = "0.4";
      string country = "country";

      node_config.NodeAddress = Utils.GenerateAHAddress().ToString();
      Utils.WriteConfig(config_path, node_config);
      SocialUtils.CreateCertificate(uid, name, pcid, version, country,
                                    node_config.NodeAddress, 
                                    node_config.Security.KeyPath);

    }

    public static string Add(string filename, string uid, string ip) {
      byte[] certData = SocialUtils.ReadFileBytes(filename);
      string cert = Convert.ToBase64String(certData);

      Dictionary<string, string> parameters = 
        new Dictionary<string, string>();

      parameters["m"] = "add";
      parameters["cert"] = cert;
      parameters["uid"] = uid;
      if (ip !=null) parameters["ip"] = ip;
      return Print(SocialUtils.Request(_url, parameters));
    }

    public static string Remove(string alias) {
      Dictionary<string, string> parameters = 
        new Dictionary<string, string>();

      parameters["m"] = "remove";
      parameters["alias"] = alias;
      return Print(SocialUtils.Request(_url, parameters));
    }

    public static string Block(string uid) {
      Dictionary<string, string> parameters = 
        new Dictionary<string, string>();

      parameters["m"] = "block";
      parameters["uid"] = uid;
      return Print(SocialUtils.Request(_url, parameters));
    }

    public static string Unblock(string uid) {
      Dictionary<string, string> parameters = 
        new Dictionary<string, string>();

      parameters["m"] = "unblock";
      parameters["uid"] = uid;
      return Print(SocialUtils.Request(_url, parameters));
    }

    public static string Login(string user, string pass) {
      Dictionary<string, string> parameters = 
        new Dictionary<string, string>();

      parameters["m"] = "login";
      parameters["user"] = user;
      parameters["pass"] = pass;
      return Print(SocialUtils.Request(_url, parameters));
    }

    public static string Logout() {
      Dictionary<string, string> parameters = 
        new Dictionary<string, string>();

      parameters["m"] = "logout";
      return Print(SocialUtils.Request(_url, parameters));
    }

    public static string GetInfo() {
      Dictionary<string, string> parameters = 
        new Dictionary<string, string>();

      parameters["m"] = "getstate";
      return Print(SocialUtils.Request(_url, parameters));
    }

    public static string Print(string output) {
      Console.WriteLine(output);
      return output;
    }

    public static void ShowHelp() {
      string help = "usage: svpncmd.exe <option> <fingerprint>\n\n" +
                    "options:\n" +
                    "  cert <jabberid> <pcid> - create cert w/pcid\n" +
                    "  login <pass> - log in user\n" +
                    "  logout - log out user\n" +
                    "  add <certfile> <uid> - add by certfile\n" +
                    "  addip <certfile> <uid> <ip> - add by certfile w/ip\n" +
                    "  remove <alias> - remove by alias\n" +
                    "  block <uid> - block by uid\n" + 
                    "  unblock <uid> - unblock by uid\n" + 
                    "  getstate - print current state in xml\n" + 
                    "  help - shows this help";
      Console.WriteLine(help);
    }

    /**
     * The main function, starting point for the program.
     */
    public static void Main(string[] args) {
      SetUrl();
      if(args.Length < 1) {
        ShowHelp();
        return;
      }
      switch (args[0]) {
        case "help":
          ShowHelp();
          break;

        case "cert":
          CreateCertificate(args[1], args[2], args[1]);
          break;

        case "login":
          Login(args[1], args[2]);
          break;

        case "logout":
          Logout();
          break;

        case "add":
          Add(args[1], args[2], null);
          break;

        case "addip":
          Add(args[1], args[2], args[3]);
          break;

        case "remove":
          Remove(args[1]);
          break;

        case "block":
          Block(args[1]);
          break;

        case "unblock":
          Unblock(args[1]);
          break;

        case "getstate":
          GetInfo();
          break;

        default:
          ShowHelp();
          break;
      }
    }
  }
}
