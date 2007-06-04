using System;
using Brunet;
using System.Text;
using System.Collections;
using System.Net.Sockets;
using System.IO;
using System.Net;

namespace Ipop 
{
  public class IPPacketHandler: IDataHandler
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
    public void HandleData(MemBlock p, ISender from, object state)
    {
      ///@todo, this copy is clearly unneeded
      byte[] packet = (byte[])p; 

      if (debug) {
        IPAddress srcAddr = IPPacketParser.SrcAddr(packet);
        IPAddress dstAddr = IPPacketParser.DestAddr(packet); 
        Console.Error.WriteLine("Incoming packet:: IP src: {0}, IP dst: {1}, p2p " +
          "from: {2}", srcAddr, dstAddr, from); 
      }

      IPAddress destAddr = IPPacketParser.DestAddr(packet);
      if (!destAddr.Equals(this.node.ip)) {
        Console.Error.WriteLine("Incoming packet not for me {0}:: IP dst: {1}", this.node.ip, destAddr);
      }
      else if(this.node.mac != null && 
        !ether.SendPacket(packet, 0x800, this.node.mac)) {
	      Console.Error.WriteLine("error writing packet from ethernet");
      }
    }
  }
}
