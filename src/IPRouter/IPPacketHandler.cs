using System;
using Brunet;
using System.Collections;
using System.Net.Sockets;
using System.IO;
using System.Net;
using System.Diagnostics;

namespace Ipop 
{
  public class IPPacketHandler: IDataHandler
  {
    Ethernet ether;
    NodeMapping node;

    public IPPacketHandler(Ethernet ethernet, NodeMapping node) {
      ether = ethernet;
      this.node = node;
    }

    public void HandleData(MemBlock p, ISender from, object state)
    {
      IPAddress destAddr = IPPacketParser.GetDestAddr(p);

      if(IPOPLog.PacketLog.Enabled)
        ProtocolLog.Write(IPOPLog.PacketLog, String.Format(
          "Incoming packet:: IP src: {0}, IP dst: {1}, p2p " +
          "from: {2}, size: {3}", IPPacketParser.GetSrcAddr(p), destAddr, 
          from, p.Length));

      if (!destAddr.Equals(this.node.ip))
        ProtocolLog.WriteIf(IPOPLog.PacketLog, String.Format(
          "Incoming packet not for me {0}:: IP dst: {1}", this.node.ip, destAddr));
      else if(this.node.mac != null && !ether.SendPacket(p, 0x800, this.node.mac))
        ProtocolLog.WriteIf(IPOPLog.PacketLog,
                            "Error writing packet from ethernet");

    }
  }
}
