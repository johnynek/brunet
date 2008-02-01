using Brunet;
using System;
using System.Collections;

namespace Ipop {
  public struct DHCPOption {
    public int type;
    public int length;
    public string encoding;
    public string string_value;
    public byte [] byte_value;
  }

  public class DecodedDHCPPacket {
    public byte op;
    public byte [] xid;
    public byte [] ciaddr;
    public byte [] yiaddr;
    public byte [] siaddr;
    public byte [] chaddr;
    public SortedList options;
    public byte [] last_ip;
    public string brunet_namespace;
    public string ipop_namespace;
    public string return_message;
    public string NodeAddress;
    public byte[] SendTo = new byte[4] {255, 255, 255, 255};
    public byte[] SendFrom = new byte[4] {0, 0, 0, 0};
  }

  public class DHCPPacket {
    public IPPacket IPPacket;
    public DecodedDHCPPacket decodedPacket;

    public DHCPPacket(IPPacket IPPacket) {
      this.IPPacket = IPPacket;
      DecodePacket();
    }

    public DHCPPacket(DecodedDHCPPacket packet) {
      this.decodedPacket = packet;
      EncodePacket();
    }

    public void DecodePacket() {
      this.decodedPacket = new DecodedDHCPPacket();
      this.decodedPacket.xid = new byte[4];
      this.decodedPacket.ciaddr = new byte[4];
      this.decodedPacket.yiaddr = new byte[4];
      this.decodedPacket.siaddr = new byte[4];
      this.decodedPacket.chaddr = new byte[6];
      MemBlock packet = (new UDPPacket(IPPacket.Payload)).Payload;
      int current = 0;
      this.decodedPacket.op = packet[current++];
      current++; /* htype should be 1 */
      current++; /* hlen should be 6 */
      current++; /* hops should be 0 */
      for(int i = 0; i < 4; i++)
        this.decodedPacket.xid[i] = packet[current + i];
      current += 4;
      current += 4; /* secs and flags can be safely ignored */
      for(int i = 0; i < 4; i++)
        this.decodedPacket.ciaddr[i] = packet[current + i];
      current += 4;
      for(int i = 0; i < 4; i++)
        this.decodedPacket.yiaddr[i] = packet[current + i];
      current += 4;
      for(int i = 0; i < 4; i++)
        this.decodedPacket.siaddr[i] = packet[current + i];
      current += 4;
      current += 4; /* qiaddr can be safely ignored */
      for(int i = 0; i < 6; i++)
        this.decodedPacket.chaddr[i] = packet[current + i];
      current += 16; /* only use first 6 bytes */
      current += 64; /* sname can be safely ignored */
      current += 128; /* file can be safely ignored */
      current += 4; /* Magic cookie */
      /* Parse the options */
      int current_option = 0;
      this.decodedPacket.options = new SortedList();
      /*  255 is end of options */
      while(packet[current] != 255) {
        /* 0 is padding */
        if(packet[current] != 0)
        {
          DHCPOption option = new DHCPOption();
          option.type = packet[current];
          /* These options are encoded using ASCII */
          if(packet[current] == 12 || packet[current] == 17 || 
            packet[current] == 14 || packet[current] == 40 || 
            packet[current] == 64 || packet[current] == 56)
            option.encoding = "string";
          else
            option.encoding = "int";
          current++;

          option.length = packet[current++];

          /* This is done, so that the server doesn't need to implement
             any ASCII character to Integer, which Python lacks         */
          if(option.encoding == "string") {
            option.byte_value = new byte[0];
            option.string_value = "";
            for(int i = 0; i < option.length; i++) 
              option.string_value += (char) packet[current++];
          }
          else {
            option.string_value = "";
            option.byte_value = new byte[option.length];
            for(int i = 0; i < option.length; i++)
              option.byte_value[i] = packet[current++];
          }
          current_option++; /* Next Option*/
          this.decodedPacket.options.Add(option.type, (DHCPOption) option);
        }
        else
          current++;
      }
    }

    public void EncodePacket() {
      /* Create the options array first, then merge the UDP header, 
         dhcp body, and dhcp options later                          */
      ArrayList byte_list = new ArrayList();
      for(int i = 0; i < decodedPacket.options.Count; i++) {
        DHCPOption option = (DHCPOption) decodedPacket.options.GetByIndex(i);
        byte_list.Add((byte) option.type);
        byte_list.Add((byte) option.length);
        if(option.encoding == "string") {
          for(int j = 0; j < option.length; j++)
            byte_list.Add((byte) ((char) option.string_value[j]));
        }
        else {
          for(int j = 0; j < option.length; j++)
            byte_list.Add((byte) option.byte_value[j]);
        }
      }
      byte [] encodedPacket = new byte[240 + byte_list.Count + 1];
      int current = 0;
      encodedPacket[current++] = this.decodedPacket.op;
      encodedPacket[current++] = 1; /* htype = ethernet */
      encodedPacket[current++] = 6; /* hlen = 6 bytes */
      encodedPacket[current++] = 0; /* hops = 0 */
      for(int i = 0; i < 4; i++)
        encodedPacket[current + i] = this.decodedPacket.xid[i];
      current += 4;
      for(int i = 0; i < 4; i++)
        encodedPacket[current + i] = 0; /* secs and flags = 0 */
      current += 4;
      for(int i = 0; i < 4; i++)
        encodedPacket[current + i] = this.decodedPacket.ciaddr[i];
      current += 4;
      for(int i = 0; i < 4; i++)
        encodedPacket[current + i] = this.decodedPacket.yiaddr[i];
      current += 4;
      for(int i = 0; i < 4; i++)
        encodedPacket[current + i] = this.decodedPacket.siaddr[i];
      current += 4;
      for(int i = 0; i < 4; i++)
        encodedPacket[current + i] = 0; /* qiaddr = 0 */
      current += 4;
      for(int i = 0; i < 6; i++)
        encodedPacket[current + i] = this.decodedPacket.chaddr[i];
      current += 6;
      for(; current < 236; current++)
        encodedPacket[current] = 0;
      /* Magic Cookie */
      encodedPacket[current++] = 99;
      encodedPacket[current++] = 130;
      encodedPacket[current++] = 83;
      encodedPacket[current++] = 99;
      byte [] temparray = (byte []) byte_list.ToArray(typeof(byte));
      temparray.CopyTo(encodedPacket, current);
      encodedPacket[encodedPacket.Length - 1] = 255; /* End of Options */

      UDPPacket udppacket = new UDPPacket(67, 68,
                                          MemBlock.Reference(encodedPacket));
      IPPacket = new IPPacket((byte) IPPacket.Protocols.UDP,
                                        decodedPacket.SendFrom,
                                        decodedPacket.SendTo,
                                        udppacket.ICPacket);
    }

    public void PrintDecodedPacket() {
      string temp;
      Console.Error.WriteLine("op : {0}", decodedPacket.op);
      temp = "";
      for(int i = 0; i < 4; i++)
        temp += this.decodedPacket.xid[i] + " ";
      Console.Error.WriteLine("xid : {0}", temp);
      temp = "";
      for(int i = 0; i < 4; i++)
        temp += this.decodedPacket.ciaddr[i] + " ";
      Console.Error.WriteLine("ciaddr : {0}", temp);
      temp = "";
      for(int i = 0; i < 4; i++)
        temp += this.decodedPacket.yiaddr[i] + " ";
      Console.Error.WriteLine("yiaddr : {0}", temp);
      temp = "";
      for(int i = 0; i < 4; i++)
        temp += this.decodedPacket.siaddr[i] + " ";
      Console.Error.WriteLine("siaddr : {0}", temp);
      temp = "";
      for(int i = 0; i < 6; i++)
        temp += this.decodedPacket.chaddr[i] + " ";
      Console.Error.WriteLine("chaddr : {0}", temp);
      for(int i = 0; i < decodedPacket.options.Count; i++) {
        DHCPOption option = (DHCPOption) decodedPacket.options.GetByIndex(i);
        string name = option.type.ToString();
        try {
           name = DHCPOptions.DHCPOptionsList[option.type];
        }
        catch {}
        if(option.encoding == "string")
          Console.Error.WriteLine("{0} : {1}", name, option.string_value);
        else {
          temp = "";
          for(int j = 0; j < option.byte_value.Length; j++)
            temp += option.byte_value[j] + " ";
          Console.Error.WriteLine("{0} : {1}", name, temp);
        }
      }
    }

    public void PrintPacket() {
      int i = 0;
      for(i = 0; i < IPPacket.Packet.Length - 3; i+=4)
        Console.Error.WriteLine("{0} {1} {2} {3}", IPPacket.Packet[i], IPPacket.Packet[i+1],
                                IPPacket.Packet[i+2], IPPacket.Packet[i+3]);
      for(; i < IPPacket.Packet.Length; i++)
        Console.Error.WriteLine("{0}", IPPacket.Packet[i]);
    }
  }
}
