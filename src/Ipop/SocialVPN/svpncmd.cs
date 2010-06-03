/*
Copyright (C) 2009 Pierre St Juste <ptony82@ufl.edu>, University of Florida

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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Brunet;
using Brunet.Applications;

namespace Ipop.SocialVPN {

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
      social_config.GlobalBlock = false;
      social_config.AutoFriend = false;
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

      parameters["m"] = "jabber.login";
      parameters["uid"] = user;
      parameters["pass"] = pass;
      return Print(SocialUtils.Request(_url, parameters));
    }

    public static string Logout() {
      Dictionary<string, string> parameters = 
        new Dictionary<string, string>();

      parameters["m"] = "jabber.logout";
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
