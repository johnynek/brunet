using System;
using Brunet;
using System.Text;
using System.Collections;
using System.Net.Sockets;
using System.IO;

namespace Ipop 
{
  public class IPPacketHandler: IAHPacketHandler
  {
    //ethernet model
    Ethernet ether;
    bool debug;
    IPAddress myAddress;
    private int count1;    
    public IPPacketHandler(Ethernet ethernet, bool debugging, IPAddress addr)
    {
      ether = ethernet;
      debug = debugging;
      myAddress = addr;
      count1 = 0;
    }
    public void HandleAHPacket(object node, AHPacket p, Edge from)
    {
	count1++;
      //Console.WriteLine("Just received a packet from Brunet...");
      //Console.WriteLine("Can read: " + p.PayloadStream.CanRead);
      byte[] packet = new byte[p.PayloadStream.Length];
      int count = p.PayloadStream.Read(packet, 0, packet.Length);
      //we need to extract the header from what we got
      IPPacketBuilder.Protocol proto = IPPacketBuilder.GetProtocol(packet);
      if (IPPacketBuilder.GetProtocol(packet) != IPPacketBuilder.Protocol.IP_PACKET) {
	Console.WriteLine("Received a non-IP packet from brunet");
      }
      packet = IPPacketBuilder.GetPayload(packet);
      if (debug) {
	IPAddress srcAddr = IPPacketParser.SrcAddr(packet);     
        IPAddress dstAddr = IPPacketParser.DestAddr(packet); 
	Console.WriteLine("Incoming packet:: IP src: {0}, IP dst: {1}, p2p hops: {2}", srcAddr, dstAddr, p.Hops); 
      }
      if (count1%30 == 0) {
	IPAddress srcAddr = IPPacketParser.SrcAddr(packet);     
        IPAddress dstAddr = IPPacketParser.DestAddr(packet); 
	//Console.WriteLine("Incoming packet:: IP src: {0}, IP dst: {1}, p2p hops: {2}", srcAddr, dstAddr, p.Hops); 
      }
      IPAddress destAddr = IPPacketParser.DestAddr(packet);
      if (!destAddr.Equals(myAddress)) {
          Console.WriteLine("Incoming packet not for me:: IP dst: {0}", destAddr);
          return;
      }

      if(!ether.SendPacket(packet)) {
	Console.WriteLine("error reading packet from ethernet");
	return;
      }
    }
    
  }
}
