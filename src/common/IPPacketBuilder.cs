/**
   The classification for all packets that we plan to send out;
**/
using System;
namespace Ipop {
  public class IPPacketBuilder {
    public enum Protocol:byte {
      IP_PACKET = 1,
	BARP_REQUEST = 2,
	BARP_REPLY = 3,
    }
    //we also provide methods for parsing the packet
    public static byte[] BuildPacket(byte[] payload, IPPacketBuilder.Protocol protocol) {
      byte[] packet = new byte[payload.Length + 1];
      packet[0] = (byte) protocol;
      Array.Copy(payload, 0, packet, 1, payload.Length);
      return packet;
    }
    public static IPPacketBuilder.Protocol GetProtocol(byte[] packet) {
      return (Protocol) packet[0];
    }
    public static byte[] GetPayload(byte[] packet) {
      byte[] payload = new byte[packet.Length - 1];
      Array.Copy(packet, 1, payload, 0, payload.Length);
      return payload;
    }
    /*    public static void Main (string []args) {
      byte[] a = new byte[10];
      byte[] packet = IPPacketBuilder.BuildPacket(a, IPPacketBuilder.Protocol.IP_PACKET);
      IPPacketBuilder.GetProtocol(packet);
      IPPacketBuilder.GetPayload(packet);
      }*/
  }
}

