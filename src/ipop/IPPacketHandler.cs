using System;
using Brunet;
using System.Text;
using System.Collections;
using System.Net.Sockets;
using System.IO;
using System.Net;

namespace Ipop 
{
  public class IPPacketHandler: IAHPacketHandler
  {
    Ethernet ether;
    bool debug;
    NodeMapping node;

    public IPPacketHandler(Ethernet ethernet, bool debugging, NodeMapping node)
    {
      ether = ethernet;
      debug = debugging;
      this.node = node;
    }
    public void HandleAHPacket(object node, AHPacket p, Edge from)
    {
      byte[] packet = new byte[p.PayloadStream.Length];
      p.PayloadStream.Read(packet, 0, packet.Length);

      if (debug) {
        IPAddress srcAddr = IPPacketParser.SrcAddr(packet);
        IPAddress dstAddr = IPPacketParser.DestAddr(packet); 
        Console.WriteLine("Incoming packet:: IP src: {0}, IP dst: {1}, p2p " +
          "hops: {2}", srcAddr, dstAddr, p.Hops); 
      }

      IPAddress destAddr = IPPacketParser.DestAddr(packet);
      if (!destAddr.Equals(this.node.ip)) {
        Console.WriteLine("Incoming packet not for me:: IP dst: {0}", destAddr);
      }
      else if(this.node.mac != null && 
        !ether.SendPacket(packet, 0x800, this.node.mac)) {
	      Console.WriteLine("error writing packet from ethernet");
      }
    }
  }
}
