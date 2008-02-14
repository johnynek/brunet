using System;
using Brunet;
using System.Collections;
using System.Net.Sockets;
using System.IO;
using System.Net;
using System.Diagnostics;

namespace Ipop 
{
  public class IPHandler: IDataHandler
  {
    protected IpopNode _node;

    public IPHandler(IpopNode node) {
      _node = node;
      _node.Brunet.GetTypeSource(PType.Protocol.IP).Subscribe(this, null);
    }

    public void HandleData(MemBlock p, ISender from, object state)
    {
      IPPacket ipp = new IPPacket(p);

      if(IPOPLog.PacketLog.Enabled) {
        ProtocolLog.Write(IPOPLog.PacketLog, String.Format(
          "Incoming packet:: IP src: {0}, IP dst: {1}, p2p " +
          "from: {2}, size: {3}", ipp.SSourceIP, ipp.SDestinationIP,
          from, p.Length));
      }

      if(_node.MAC != null && !_node.Ether.Write(p, EthernetPacket.Types.IP, _node.MAC)) {
        ProtocolLog.WriteIf(IPOPLog.PacketLog,
                            "Error writing packet from ethernet");
      }
    }

    public void Send(AHAddress target, MemBlock p) {
      ISender s = new AHExactSender(_node.Brunet, target);
      s.Send(new CopyList(PType.Protocol.IP, p));
    }
  }
}
