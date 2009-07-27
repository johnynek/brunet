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
  public class SVPNTool {

    public const string URL = "http://127.0.0.1:58888/api";

    public static bool CreateCertificate(string uid, string pcid, string name) {
      string config_path = "brunet.config";
      NodeConfig node_config = Utils.ReadConfig<NodeConfig>(config_path);
      if(!System.IO.Directory.Exists(node_config.Security.CertificatePath)) {
        string version = SocialNode.VERSION;
        string country = "undefined";
        

        node_config.NodeAddress = Utils.GenerateAHAddress().ToString();
        Utils.WriteConfig(config_path, node_config);
        SocialUtils.CreateCertificate(uid, name, pcid, version, country,
                                      node_config.NodeAddress, 
                                      node_config.Security.CertificatePath,
                                      node_config.Security.KeyPath);
        Console.WriteLine("Certificate creation successful");
        return true;
      }
      return false;
    }

    public static void MakeCall(string method, string fprs) {
      Dictionary<string, string> parameters = 
        new Dictionary<string, string>();

      parameters["m"] = method;
      parameters["fprs"] = fprs;
      try {
        SocialUtils.Request(URL, parameters);
      } catch(Exception e) {
        Console.WriteLine(e.Message);
        Console.WriteLine("Could not connect to SocialVPN, make sure" +
                          "process is running");
      }
      System.Threading.Thread.Sleep(2000);
      GetState();
    }

    public static void GetState() {
      PrintState(SocialUtils.Request(URL));
    }

    public static void PrintState(string stateString) {
      SocialState state = SocialUtils.XmlToObject<SocialState>(stateString);
      string header = String.Format("{0} - {1} - {2}\nYour fingerprint - {3}",
                                    state.LocalUser.Name,
                                    state.LocalUser.Alias, 
                                    state.LocalUser.IP,
                                    state.LocalUser.DhtKey);
      Console.WriteLine("\n" + header + "\n");
      Console.WriteLine("{0,-20}{1,-30}{2,-16}{3,-10}{4,-50}",
                          "Name", "Alias", "IP Address", "Status", 
                          "Fingerprint");
      foreach(SocialUser friend in state.Friends) {
        string status = "Online";
        if(friend.Time == "0" & friend.Access == "Allow") {
          status = "Offline";
        }
        else if(friend.Access == "Block") {
          status = "Blocked";
        }
        Console.WriteLine("{0,-20}{1,-30}{2,-16}{3,-10}{4,-50}",
                          friend.Name, friend.Alias, friend.IP, 
                          status, friend.DhtKey);
      }
    }

    public static void ShowHelp() {
      string help = "usage: SVPNTool.exe <option> <fingerprint>\n\n" +
                    "options:\n" +
                    "  friends - shows current user info and friends\n" +
                    "  add <emails> - add a friend by email address\n" +
                    "  addfpr <fprs> - add a friend by fingerprint\n" +
                    "  unblock <fprs> - unblock a friend by fingerprint\n" +
                    "  block <fprs> - block a friend by fingerprint\n" + 
                    "  delete <fprs> - remove a friend from the list\n" +
                    "  global <on/off> - set global access (automatic links)\n" +
                    "  help - shows this help";
      Console.WriteLine(help);
    }

    /**
     * The main function, starting point for the program.
     */
    public static new void Main(string[] args) {
      if(args.Length == 0) {
        ShowHelp();
      }
      else if(args[0] == "help") {
        ShowHelp();
      }
      else if(args[0] == "friends") {
        GetState();
      }
      else if(args[0] == "cert") {
        CreateCertificate(args[1], args[2], args[3]);
      }
      else {
        MakeCall(args[0], args[1]);
      }
    }
  }
}
