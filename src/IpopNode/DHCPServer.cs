/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using NetworkPackets;
using NetworkPackets.DHCP;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

#if NUNIT
using NUnit.Framework;
#endif

namespace Ipop {
  public abstract class DHCPServer {
    /// <summary>The Server's IP Address</summary>
    public readonly byte[] ServerIP;
    /// <summary>The broadcast address for the network.</summary>
    public byte[] Broadcast;
    /// <summary>The netmask for the namespace.</summary>
    public readonly byte [] Netmask;
    /// <summary>Base IP Address.</summary>
    public readonly byte[] BaseIP;
    /// <summary>The DHCPConfig that defines this controller.</summary>
    public readonly DHCPConfig Config;

    /// <summary>Random number generator to guess IP Addresses</summary>
    protected Random _rand = new Random();
    /// <summary>A list of reserved IPs</summary>
    protected byte [][] _reserved_ips;
    /// <summary>Netmasks mapped to the list of reserved IPs.</summary>
    protected byte [][] _reserved_masks;

    protected MemBlock _lease_time;
    public const int MTU = 1200;
    protected MemBlock _mtu;

    /// <summary></summary>
    public DHCPServer(DHCPConfig config) {
      Config = config;
      Netmask = Utils.StringToBytes(config.Netmask, '.');
      BaseIP = Utils.StringToBytes(config.IPBase, '.');

      // just in case someone is cute
      for(int i = 0; i < BaseIP.Length; i++) {
        BaseIP[i] = (byte) (Netmask[i] & BaseIP[i]);
      }

      if(config.ReservedIPs != null) {
        _reserved_ips = new byte[config.ReservedIPs.Length + 2][];
        _reserved_masks = new byte[config.ReservedIPs.Length + 2][];
        for(int i = 0; i < config.ReservedIPs.Length; i++) {
          _reserved_ips[i] = Utils.StringToBytes(config.ReservedIPs[i].IPBase, '.');
          _reserved_masks[i] = Utils.StringToBytes(config.ReservedIPs[i].Mask, '.');
        }
      } else {
        _reserved_ips = new byte[2][];
        _reserved_masks = new byte[2][];
      }

      // reserve broadcast and server ip
      int last = _reserved_ips.Length - 1;
      int slast = last - 1;
      _reserved_ips[last] = new byte[4];
      _reserved_ips[slast] = new byte[4];

      for(int i = 0; i < 4; i++) {
        _reserved_ips[slast][i] = (byte) (BaseIP[i] | ~Netmask[i]);
      }

      Broadcast = _reserved_ips[slast];
      ServerIP = new byte[4];
      BaseIP.CopyTo(ServerIP, 0);
      ServerIP[3] = 1;
      _reserved_ips[last] = BaseIP;
      _reserved_masks[last] = new byte[4] {255, 255, 255, 254};
      _reserved_masks[slast] = new byte[4] {255, 255, 255, 255};

      _mtu = MemBlock.Reference(new byte[2] {
          (byte) ((MTU >> 8) & 0xFF),
          (byte) (MTU & 0xFF)
        });

      _lease_time = MemBlock.Reference(new byte[4] {
          (byte) (Config.LeaseTime >> 24),
          (byte) (Config.LeaseTime >> 16),
          (byte) (Config.LeaseTime >> 8),
          (byte) (Config.LeaseTime),
        });
    }

    /// <summary></summary>
    public DHCPPacket ProcessPacket(DHCPPacket packet, string unique_id,
        byte[] last_ip, params object[] dhcp_params)
    {
      DHCPPacket.MessageTypes message_type = (DHCPPacket.MessageTypes)
                                  packet.Options[DHCPPacket.OptionTypes.MESSAGE_TYPE][0];

      byte[] requested_ip = last_ip;
      bool renew = false;

      if(message_type == DHCPPacket.MessageTypes.DISCOVER) {
        message_type = DHCPPacket.MessageTypes.OFFER;
      } else if(message_type == DHCPPacket.MessageTypes.REQUEST) {
        if(packet.Options.ContainsKey(DHCPPacket.OptionTypes.REQUESTED_IP)) {
          requested_ip = packet.Options[DHCPPacket.OptionTypes.REQUESTED_IP];
        } else if(!packet.ciaddr.Equals(IPPacket.ZeroAddress)) {
          requested_ip = packet.ciaddr;
        }
        renew = true;
        message_type = DHCPPacket.MessageTypes.ACK;
      } else {
        throw new Exception("Unsupported message type!");
      }

      byte[] reply_ip = RequestLease(requested_ip, renew, unique_id, dhcp_params);

      Dictionary<DHCPPacket.OptionTypes, MemBlock> options =
        new Dictionary<DHCPPacket.OptionTypes, MemBlock>();

      options[DHCPPacket.OptionTypes.DOMAIN_NAME] = Encoding.UTF8.GetBytes(DNS.DomainName);
//  The following option is needed for dhcp to "succeed" in Vista, but they break Linux
//    options[DHCPPacket.OptionTypes.ROUTER] = reply.ip;
      options[DHCPPacket.OptionTypes.DOMAIN_NAME_SERVER] = MemBlock.Reference(ServerIP);
      options[DHCPPacket.OptionTypes.SUBNET_MASK] = MemBlock.Reference(Netmask);
      options[DHCPPacket.OptionTypes.LEASE_TIME] = _lease_time;
      options[DHCPPacket.OptionTypes.MTU] = _mtu;
      options[DHCPPacket.OptionTypes.SERVER_ID] = MemBlock.Reference(ServerIP);
      options[DHCPPacket.OptionTypes.MESSAGE_TYPE] = MemBlock.Reference(new byte[]{(byte) message_type});
      DHCPPacket rpacket = new DHCPPacket(2, packet.xid, packet.ciaddr, reply_ip,
                               ServerIP, packet.chaddr, options);
      return rpacket;
    }

    /// <summary>Makes sure that an IP Address is valid.</summary>
    /// <param name="ip">Checks to see if an IP Address is valid.</param>
    public bool ValidIP(byte [] ip) {
      // Check range
      for(int i = 0; i < ip.Length; i++) {
        if((ip[i] & Netmask[i]) != BaseIP[i]) {
          return false;
        }
      }

      // Check Reserved
      for(int i = 0; i < _reserved_ips.Length; i++) {
        for(int j = 0; j < _reserved_ips[i].Length; j++) {
          if((ip[j] & _reserved_masks[i][j]) != (_reserved_ips[i][j] & _reserved_masks[i][j])) {
            break;
          } else if(j == _reserved_ips[i].Length - 1) {
            return false;
          }
        }
      }
      return true;
    }

    /// <summary>Increments the inputted IP Address to the next valid one.</summary>
    /// <param name="ip">The IP Address to increment.</param>
    public byte [] IncrementIP(byte [] ip) {
      MemBlock start = MemBlock.Reference(ip);
      byte[] new_ip = new byte[4];
      ip.CopyTo(new_ip, 0);

      do {
        if(new_ip[3] == Broadcast[3]) {
          new_ip[3] = BaseIP[3];
          if(new_ip[2] == Broadcast[2]) {
            new_ip[2] = BaseIP[2];
            if(new_ip[1] == Broadcast[1]) {
              new_ip[1] = BaseIP[1];
              if(new_ip[0] == Broadcast[0]) {
                new_ip[0] = BaseIP[0];
              } else {
                new_ip[0]++;
              }
            } else {
              new_ip[1]++;
            }
          } else {
            new_ip[2]++;
          }
        } else {
          new_ip[3]++;
        }
      } while(!ValidIP(new_ip) && !MemBlock.Reference(new_ip).Equals(start));

      return new_ip;
    }

    /// <summary>Generates a random IP Address in the valid address range.</summary>
    protected byte[] GenerateRandomIPAddress() {
      byte[] random_ip = new byte[4];
      for (int k = 0; k < random_ip.Length; k++) {
        int max = Broadcast[k];
        int min = BaseIP[k];
        random_ip[k] = (byte) _rand.Next(min, max + 1);
      }
      return random_ip;
    }

    /// <summary>This attempts to generate a valid Random IPAddress and throws an
    /// exception, if after 100 tries, it still has not generated a valid IP
    /// Address.</summary>
    public byte[] RandomIPAddress() {
      int i = 100;
      while(i-- > 0) {
        byte[] ip = GenerateRandomIPAddress();
        if(ValidIP(ip)) {
          return ip;
        }
      }
      throw new Exception("Unable to generate a random IP Address");
    }

    /// <summary>Request a lease using the given IP.</summary>
    /// <remarks>When Renew is true, the operation will only return a valid lease
    /// if the operation is successful with the requested address.  If renew is
    /// false but address is not null, then it should attempt at least once to
    /// acquire that address prior to moving on.</remarks>
    /// <param name="address">A requested IP Address</param>
    /// <param name="renew">Is this an attempt to renew?</param>
    /// <param name="node_address">The unique identifier for this node, such as a
    /// Node Address.</param>
    /// <param name="para">Extra parameters.</param>
    public abstract byte[] RequestLease(byte[] address, bool renew,
                                       string node_address, params object[] para);
  }
#if NUNIT
  [TestFixture]
  public class DHCPTest {
    public static DHCPConfig BasicConfig() {
      DHCPConfig config = new DHCPConfig();
      config.LeaseTime = 10;
      config.Namespace = "Test";
      return config;
    }

    [Test]
    public void Test() {
      DHCPConfig config = BasicConfig();
      config.Netmask = "128.0.0.0";
      config.IPBase = "128.0.0.0";
      config.ReservedIPs = new DHCPConfig.ReservedIP[1];
      config.ReservedIPs[0] = new DHCPConfig.ReservedIP();
      config.ReservedIPs[0].IPBase = "130.0.0.0";
      config.ReservedIPs[0].Mask = "255.0.0.0";

      TestDHCPServer ds = new TestDHCPServer(config);
      byte[] ip = new byte[4] { 128, 0, 0, 1};

      Assert.IsFalse(ds.ValidIP(ip), "Server IP");
      ip[0] = 129;
      Assert.IsTrue(ds.ValidIP(ip), "Non-Server IP");

      ip = new byte[4] { 130, 0, 0, 0};
      ip[0] = 130;
      byte[] inc = ds.IncrementIP(ip);
      ip[0] = 131;
      Assert.AreEqual(MemBlock.Reference(ip), MemBlock.Reference(inc), "Invalid IPs before the first valid one");

      ip[0] = 255;
      ip[1] = 255;
      ip[2] = 255;
      ip[3] = 254;
      inc = ds.IncrementIP(ip);
      ip[0] = 128;
      ip[1] = 0;
      ip[2] = 0;
      ip[3] = 2;
      Assert.AreEqual(MemBlock.Reference(ip), MemBlock.Reference(inc), "Skip broadcast and server");

      ip[0] = 127;
      Assert.IsFalse(ds.ValidIP(ip), "Out of range - 127");
      ip[0] = 0;
      Assert.IsFalse(ds.ValidIP(ip), "Out of range - 0");

      Assert.IsTrue(ds.ValidIP(ds.RandomIPAddress()), "Random");
    }

    [Test]
    public void SmallTest() {
      DHCPConfig config = BasicConfig();
      config.Netmask = "255.255.255.0";
      config.IPBase = "128.250.3.0";
      config.ReservedIPs = new DHCPConfig.ReservedIP[1];
      config.ReservedIPs[0] = new DHCPConfig.ReservedIP();
      config.ReservedIPs[0].IPBase = "128.250.3.22";
      config.ReservedIPs[0].Mask = "255.255.255.255";

      TestDHCPServer ds = new TestDHCPServer(config);
      byte[] ip = new byte[4] { 128, 250, 3, 1};

      Assert.IsFalse(ds.ValidIP(ip), "Server IP");
      ip[3] = 2;
      Assert.IsTrue(ds.ValidIP(ip), "Non-Server IP");

      ip[3] = 21;
      byte[] inc = ds.IncrementIP(ip);
      ip[3] = 23;
      Assert.AreEqual(MemBlock.Reference(ip), MemBlock.Reference(inc), "Invalid IPs before the first valid one");

      ip[0] = 128;
      ip[1] = 250;
      ip[2] = 3;
      ip[3] = 254;
      inc = ds.IncrementIP(ip);
      ip[0] = 128;
      ip[1] = 250;
      ip[2] = 3;
      ip[3] = 2;
      Assert.AreEqual(MemBlock.Reference(ip), MemBlock.Reference(inc), "Skip broadcast and server");

      ip[2] = 127;
      Assert.IsFalse(ds.ValidIP(ip), "Out of range - 127");
      ip[2] = 0;
      Assert.IsFalse(ds.ValidIP(ip), "Out of range - 0");
      Assert.IsTrue(ds.ValidIP(ds.RandomIPAddress()), "Random");
    }
  }


  public class TestDHCPServer : DHCPServer {
    public TestDHCPServer(DHCPConfig config) : base(config) { }

    public override byte[] RequestLease(byte[] address, bool renew,
                                       string node_address, params object[] para)
    {
      return null;
    }
  }
#endif
}

