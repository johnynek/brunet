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
    //ethernet model
    Ethernet ether;
    bool debug;
    IPAddress myAddress;
#if IPOP_RECEIVE_DEBUG
    private int count1;
#endif
    public IPPacketHandler(Ethernet ethernet, bool debugging, IPAddress addr)
    {
      ether = ethernet;
      debug = debugging;
      myAddress = addr;
#if IPOP_RECEIVE_DEBUG
      count1 = 0;
#endif
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

#if IPOP_RECEIVE_DEBUG
      count1++;
      if (count1 == 1000) {
        IPAddress srcAddr = IPPacketParser.SrcAddr(packet);
        IPAddress dstAddr = IPPacketParser.DestAddr(packet);
        Console.WriteLine("Incoming packet:: IP src: {0}, IP dst: {1}, " +
          "p2p hops: {2}", srcAddr, dstAddr, p.Hops); 
        count1 = 0;
      }
#endif

      IPAddress destAddr = IPPacketParser.DestAddr(packet);
      if (!destAddr.Equals(myAddress)) {
          Console.WriteLine("Incoming packet not for me:: IP dst: {0}", destAddr);
          return;
      }

      if(!ether.SendPacket(packet, 0x800)) {
	Console.WriteLine("error reading packet from ethernet");
	return;
      }
    }
  }
}
