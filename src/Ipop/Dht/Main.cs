/*
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet;
using Brunet.Applications;
using Brunet.Security;
using NDesk.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Ipop.Dht {
  public class Runner {
    public static DhtIpopNode CurrentNode { get { return _current_node; } }
    protected static DhtIpopNode _current_node;

    public static int Main(String[] args) {
      string node_config_path = string.Empty;
      string ipop_config_path = string.Empty;
      string dhcp_config_path = string.Empty;
      bool show_help = false;

      OptionSet opts = new OptionSet() {
        { "n|NodeConfig=", "Path to a NodeConfig file.",
          v => node_config_path = v },
        { "i|IpopConfig=", "Path to an IpopConfig file.",
          v => ipop_config_path = v },
        { "d|DhcpConfig=", "Path to a DHCPConfig file.",
          v => dhcp_config_path = v },
        { "h|help", "Display this help and exit.",
          v => show_help = v != null },
      };

      try {
        opts.Parse(args);
      } catch (OptionException e) {
        PrintError(e.Message);
        return -1;
      }

      if(show_help) {
        ShowHelp(opts);
        return -1;
      }

      if(node_config_path == string.Empty || !File.Exists(node_config_path)) {
        PrintError("Missing NodeConfig");
        return -1;
      }

      if(ipop_config_path == string.Empty || !File.Exists(ipop_config_path)) {
        PrintError("Missing IpopConfig");
        return -1;
      }

      ConfigurationValidator cv = new ConfigurationValidator();
      NodeConfig node_config = null;
      try {
        cv.Validate(node_config_path, "Node.xsd");
        node_config = Utils.ReadConfig<NodeConfig>(node_config_path);
        node_config.Path = node_config_path;
      } catch (Exception e) {
        Console.WriteLine("Invalid NodeConfig file:");
        Console.WriteLine("\t" + e.Message);
        return -1;
      }

      if(node_config.NodeAddress == null) {
        node_config.NodeAddress = (Utils.GenerateAHAddress()).ToString();
        node_config.WriteConfig();
      }

      IpopConfig ipop_config = null;
      try {
        cv.Validate(ipop_config_path, "Ipop.xsd");
        ipop_config = Utils.ReadConfig<IpopConfig>(ipop_config_path);
        ipop_config.Path = ipop_config_path;
      } catch (Exception e) {
        Console.WriteLine("Invalid IpopConfig file:");
        Console.WriteLine("\t" + e.Message);
        return -1;
      }

      DHCPConfig dhcp_config = null;
      if(dhcp_config_path != string.Empty) {
        if(!File.Exists(dhcp_config_path)) {
          PrintError("No such DhtIpop file");
          return -1;
        }
        try {
          cv.Validate(dhcp_config_path, "Dhcp.xsd");
          dhcp_config = Utils.ReadConfig<DHCPConfig>(dhcp_config_path);
        } catch(Exception e) {
          Console.WriteLine("Invalid DhcpConfig file:");
          Console.WriteLine("\t" + e.Message);
          return -1;
        }

        if(!dhcp_config.Namespace.Equals(ipop_config.IpopNamespace)) {
          PrintError("IpopConfig.Namespace isn't the same as DHCPConfig.Namespace");
          return -1;
        }
      }

      bool groupvpn = node_config.Security.Enabled &&
        ipop_config.GroupVPN.Enabled &&
        ipop_config.EndToEndSecurity;
      
      // enable GroupVPN!
      if(groupvpn) {
        // check to see if we have a valid private key
        RSACryptoServiceProvider public_key = new RSACryptoServiceProvider();
        bool create = true;
        if(File.Exists(node_config.Security.KeyPath)) {
          try {
            using(FileStream fs = File.Open(node_config.Security.KeyPath, FileMode.Open)) {
              byte[] blob = new byte[fs.Length];
              fs.Read(blob, 0, blob.Length);
              public_key.ImportCspBlob(blob);
              public_key.ImportCspBlob(public_key.ExportCspBlob(false));
            }
            create = false;
          } catch { }
        }

        // we don't, let's create one
        if(create) {
          using(FileStream fs = File.Open(node_config.Security.KeyPath, FileMode.Create)) {
            RSACryptoServiceProvider private_key = new RSACryptoServiceProvider(2048);
            byte[] blob = private_key.ExportCspBlob(true);
            fs.Write(blob, 0, blob.Length);
            public_key.ImportCspBlob(private_key.ExportCspBlob(false));
          }
        }

        // verify we have a cacert
        string cacert_path = Path.Combine(node_config.Security.CertificatePath, "cacert");
        if(!File.Exists(cacert_path)) {
          Console.WriteLine("DhtIpop: ");
          Console.WriteLine("\tMissing CACert: " + cacert_path);
        }

        // do we already have a certificate that matches our node id?
        string cert_path = Path.Combine(node_config.Security.CertificatePath,
            "lc." + node_config.NodeAddress.Substring(12));
        // no, let's create one
        if(create || !File.Exists(cert_path)) {
          // prepare access to the groupvpn site
          string webcert_path = Path.Combine(node_config.Security.CertificatePath, "webcert");
          if(!File.Exists(webcert_path)) {
            Console.WriteLine("DhtIpop: ");
            Console.WriteLine("\tMissing Servers signed cert: " + webcert_path);
          }

          X509Certificate webcert = X509Certificate.CreateFromCertFile(webcert_path);
          CertificatePolicy.Register(webcert);

          // get certificate and store it to file
          GroupVPNClient gvc = new GroupVPNClient(ipop_config.GroupVPN.UserName,
              ipop_config.GroupVPN.Group, ipop_config.GroupVPN.Secret,
              ipop_config.GroupVPN.ServerURI, node_config.NodeAddress,
              public_key);
          gvc.Start();
          if(gvc.State != GroupVPNClient.States.Finished) {
            Console.WriteLine("DhtIpop: ");
            Console.WriteLine("\tFailure attempting to use GroupVPN");
            return -1;
          }

          using(FileStream fs = File.Open(cert_path, FileMode.Create)) {
            byte[] blob = gvc.Certificate.X509.RawData;
            fs.Write(blob, 0, blob.Length);
          }
        }
      }

      if(dhcp_config != null) {
        _current_node = new DhtIpopNode(node_config, ipop_config, dhcp_config);
      } else {
        _current_node = new DhtIpopNode(node_config, ipop_config);
      }
      
      if(groupvpn) {
        // hack until I can come up with a cleaner solution to add features
        // that don't break config files on previous IPOP
        string cacert_path = Path.Combine(node_config.Security.CertificatePath, "cacert");
        string revocation_url = ipop_config.GroupVPN.ServerURI.Replace("mono/GroupVPN.rem",
            "data/" + ipop_config.GroupVPN.Group + "/revocation_list");
        revocation_url = revocation_url.Replace("https", "http");
        var icv = new GroupCertificateVerification(revocation_url, cacert_path);
        _current_node.Bso.CertificateHandler.AddCertificateVerification(icv);
        icv.RevocationUpdate += _current_node.Bso.CheckSAs;
      }

      Console.WriteLine("Starting IPOP: " + DateTime.UtcNow);
      _current_node.Run();
      return 0;
    }

    public static void ShowHelp(OptionSet p) {
      Console.WriteLine("Usage: DhtIpop --IpopConfig=filename --NodeConfig=filename");
      Console.WriteLine("DhtIpop - Virtual Networking Daemon.");
      Console.WriteLine();
      Console.WriteLine("Options:");
      p.WriteOptionDescriptions(Console.Out);
    }

    public static void PrintError(string error) {
      Console.WriteLine("DhtIpop: ");
      Console.WriteLine("\t" + error);
      Console.WriteLine("Try `DhtIpop.exe --help' for more information.");
    }
  }
}
