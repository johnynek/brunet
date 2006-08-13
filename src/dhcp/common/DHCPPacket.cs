using System;
using System.Collections;

namespace Ipop {
  public class DHCPPacket {
    public byte [] packet;
    public DecodedDHCPPacket decodedPacket;

    public DHCPPacket(byte [] packet) { this.packet = packet; }

    public DHCPPacket(DecodedDHCPPacket packet) { this.decodedPacket = packet;  }

    public void DecodePacket() {
      this.decodedPacket = new DecodedDHCPPacket();
      this.decodedPacket.xid = new byte[4];
      this.decodedPacket.ciaddr = new byte[4];
      this.decodedPacket.yiaddr = new byte[4];
      this.decodedPacket.siaddr = new byte[4];
      this.decodedPacket.chaddr = new byte[6];
      int current = 28;
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
              option.string_value += (char) packet[current + i];
          }
          else {
            option.string_value = "";
            option.byte_value = new byte[option.length];
            for(int i = 0; i < option.length; i++)
              option.byte_value[i] = packet[current + i];
          }
          current += option.length;
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
      byte [] encodedPacket = new byte[268 + byte_list.Count + 1];
      byte [] temp = DHCPPacket.GenerateHeader(encodedPacket.Length);
      for (int i = 0; i < 28; i++)
        encodedPacket[i] = temp[i];
      int current = 28;
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
      for(; current < 264; current++)
        encodedPacket[current] = 0;
      /* Magic Cookie */
      encodedPacket[current++] = 99;
      encodedPacket[current++] = 130;
      encodedPacket[current++] = 83;
      encodedPacket[current++] = 99;
      byte [] temparray = (byte []) byte_list.ToArray(typeof(byte));
      for(; current < encodedPacket.Length - 1; current++)
        encodedPacket[current] = temparray[current - 268];
      encodedPacket[encodedPacket.Length - 1] = 255; /* End of Options */
      packet = encodedPacket;
    }

    public static byte [] GenerateHeader(int length) {
/*  00 means that the data needs to be generated, otherwise the data is static
    Length of the datagram (ip header (20) + Length of UDP)
    Header checksum (sum of all 16 bit words inverted)
    Length UDP Header (8 bytes) + encapsulated data */
      byte [] ip_header = new byte[28] {69, 16, 00, 00, 0, 0, 0, 0, 64, 17,
        00, 00, 0, 0, 0, 0, 255, 255, 255, 255, 00, 00, 00, 00, 00, 00, 00,
        00};
      byte [] udp_header = new byte[8] {0, 67, 0, 68, 00, 00, 0, 0};
      int udp_length = length - 20;
      int ip_length = length;
      byte lbyte = 0, rbyte = 0;
      DHCPPacket.half_to_bytes(udp_length, ref lbyte, ref rbyte);
      udp_header[4] = lbyte;
      udp_header[5] = rbyte;
      DHCPPacket.half_to_bytes(ip_length, ref lbyte, ref rbyte);
      ip_header[2] = lbyte;
      ip_header[3] = rbyte;
      int checksum = DHCPPacket.getChecksum(ip_header);
      DHCPPacket.half_to_bytes(checksum, ref lbyte, ref rbyte);
      ip_header[10] = lbyte;
      ip_header[11] = rbyte;
/*    Combine the two packets */
      for(int i = 0; i < 8; i++)
        ip_header[20 + i] = udp_header[i];
      return ip_header;
    }

    public static void half_to_bytes(int half, ref byte lbyte, 
      ref byte rbyte) {
      rbyte = (byte) (half & 255);
      lbyte = (byte) ((half >> 8) & 255);
    }

    public static int getChecksum(byte [] data) {
      int value = 0;
      for(int i = 0; i < data.Length; i+=2)
        value += data[i+1] + (data[i] << 8);
      return (0xFFFF - (value & 0xFFFF) - 2);
    }

    public void PrintDecodedPacket() {
      string temp;
      Console.WriteLine("op : {0}", decodedPacket.op);
      temp = "";
      for(int i = 0; i < 4; i++)
        temp += this.decodedPacket.xid[i] + " ";
      Console.WriteLine("xid : {0}", temp);
      temp = "";
      for(int i = 0; i < 4; i++)
        temp += this.decodedPacket.ciaddr[i] + " ";
      Console.WriteLine("ciaddr : {0}", temp);
      temp = "";
      for(int i = 0; i < 4; i++)
        temp += this.decodedPacket.yiaddr[i] + " ";
      Console.WriteLine("yiaddr : {0}", temp);
      temp = "";
      for(int i = 0; i < 4; i++)
        temp += this.decodedPacket.siaddr[i] + " ";
      Console.WriteLine("siaddr : {0}", temp);
      temp = "";
      for(int i = 0; i < 6; i++)
        temp += this.decodedPacket.chaddr[i] + " ";
      Console.WriteLine("chaddr : {0}", temp);
      for(int i = 0; i < decodedPacket.options.Count; i++) {
        DHCPOption option = (DHCPOption) decodedPacket.options.GetByIndex(i);
        if(option.encoding == "string")
          Console.WriteLine("{0} : {1}", 
            DHCPOptions.DHCPOptionsList[option.type], option.string_value);
        else {
          temp = "";
          for(int j = 0; j < option.length; j++)
            temp += option.byte_value[j] + " ";
          Console.WriteLine("{0} : {1}", DHCPOptions.DHCPOptionsList[
            option.type], temp);
        }
      }
    }

    public void PrintPacket() {
      int i = 0;
      for(i = 0; i < this.packet.Length - 3; i+=4)
        Console.WriteLine("{0} {1} {2} {3}", this.packet[i], this.packet[i+1],
          this.packet[i+2], this.packet[i+3]);
          for(; i < this.packet.Length; i++)
        Console.WriteLine("{0}", this.packet[i]);
    }
  }
}