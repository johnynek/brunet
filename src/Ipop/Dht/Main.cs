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
  public class DhtNodeParameters : RuntimeParameters {
    public const string DHCP_XSD = "Dhcp.xsd";
    public const string IPOP_XSD = "Ipop.xsd";

    public IpopConfig IpopConfig { get { return _ipop_config; } }
    public DHCPConfig DhcpConfig { get { return _dhcp_config; } }
    public bool GroupVPN { get { return _group_vpn; } }

    protected IpopConfig _ipop_config;
    protected string _ipop_config_path = string.Empty;
    protected DHCPConfig _dhcp_config;
    protected string _dhcp_config_path = string.Empty;
    protected bool _group_vpn;

    public DhtNodeParameters() :
      base("DhtIpop", "DhtIpop - Virtual Networking Daemon.")
    {
      _options.Add("i|IpopConfig=", "Path to an IpopConfig file.",
          v => _ipop_config_path = v);
      _options.Add("d|DhcpConfig=", "Path to a DHCPConfig file.",
          v => _dhcp_config_path = v);
    }

    public override int Parse(string[] args)
    {
      if(base.Parse(args) != 0) {
        return -1;
      }

      if(_ipop_config_path == string.Empty || !File.Exists(_ipop_config_path)) {
        _error_message = "Missing IpopConfig";
        return -1;
      }

      try {
        Validator.Validate(_ipop_config_path, IPOP_XSD);
        _ipop_config = Utils.ReadConfig<IpopConfig>(_ipop_config_path);
        _ipop_config.Path = _ipop_config_path;
      } catch (Exception e) {
        _error_message = "Invalid IpopConfig file:" + e.Message;
        return -1;
      }

      if(_dhcp_config_path != string.Empty) {
        if(!File.Exists(_dhcp_config_path)) {
          _error_message = "No such DhtIpop file";
          return -1;
        }

        try {
          Validator.Validate(_dhcp_config_path, DHCP_XSD);
          _dhcp_config = Utils.ReadConfig<DHCPConfig>(_dhcp_config_path);
        } catch(Exception e) {
          _error_message = "Invalid DhcpConfig file: " + e.Message;
          return -1;
        }

        if(!_dhcp_config.Namespace.Equals(_ipop_config.IpopNamespace)) {
          _error_message = "IpopConfig.Namespace isn't the same as DHCPConfig.Namespace";
          return -1;
        }
      }

      _group_vpn = _node_config.Security.Enabled &&
        _ipop_config.GroupVPN.Enabled &&
        _ipop_config.EndToEndSecurity;
      return 0;
    }
  }

  public class Runner {
    public static DhtIpopNode CurrentNode { get { return _current_node; } }
    protected static DhtIpopNode _current_node;

    public static int Main(string[] args)
    {
      DhtNodeParameters parameters= new DhtNodeParameters();
      if(parameters.Parse(args) != 0) {
        Console.WriteLine(parameters.ErrorMessage);
        parameters.ShowHelp();
        return -1;
      } else if(parameters.Help) {
        parameters.ShowHelp();
        return 0;
      }

      DHCPConfig dhcp_config = parameters.DhcpConfig;
      IpopConfig ipop_config = parameters.IpopConfig;
      NodeConfig node_config = parameters.NodeConfig;


      if(node_config.NodeAddress == null) {
        node_config.NodeAddress = (Utils.GenerateAHAddress()).ToString();
        node_config.WriteConfig();
      }

      if(parameters.GroupVPN) {
        // check to see if we have a valid private key
        RSACryptoServiceProvider public_key = new RSACryptoServiceProvider();
        bool create = true;
        // If this succeeds, the key is good, if it fails, we need to create a new one...
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
          Console.WriteLine("Missing CACert: " + cacert_path);
          parameters.ShowHelp();
          return -1;
        }

        // do we already have a certificate that matches our node id?
        string cert_path = Path.Combine(node_config.Security.CertificatePath,
            "lc." + node_config.NodeAddress.Substring(12));
        // no, let's create one
        if(create || !File.Exists(cert_path)) {
          // prepare access to the groupvpn site
          string webcert_path = Path.Combine(node_config.Security.CertificatePath, "webcert");
          if(!File.Exists(webcert_path)) {
            Console.WriteLine("Missing Servers signed cert: " + webcert_path);
            parameters.ShowHelp();
            return -1;
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
            Console.WriteLine("Failure attempting to use GroupVPN");
            parameters.ShowHelp();
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
      
      if(parameters.GroupVPN) {
        // hack until I can come up with a cleaner solution to add features
        // that don't break config files on previous IPOP
        string cacert_path = Path.Combine(node_config.Security.CertificatePath, "cacert");
        string revocation_url = ipop_config.GroupVPN.ServerURI.Replace("mono/GroupVPN.rem",
            "data/" + ipop_config.GroupVPN.Group + "/revocation_list");
        revocation_url = revocation_url.Replace("https", "http");
        var icv = new GroupCertificateVerification(revocation_url, cacert_path);
        _current_node.AppNode.SecurityOverlord.CertificateHandler.AddCertificateVerification(icv);
        icv.RevocationUpdate += _current_node.AppNode.SecurityOverlord.VerifySAs;
      }

      Console.WriteLine("Starting IPOP: " + DateTime.UtcNow);
      _current_node.Run();
      return 0;
    }
  }
}
