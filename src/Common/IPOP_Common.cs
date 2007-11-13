using Brunet;
using System.Net;
using System.Security.Cryptography;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Sockets;

namespace Ipop {
  public class IPOP_Common {
/* Converts IP Address into Brunet Address */
    public static BigInteger GetHash(IPAddress addr) {
       HashAlgorithm hashAlgo = HashAlgorithm.Create();
       byte[] hash = hashAlgo.ComputeHash(addr.GetAddressBytes());
       hash[Address.MemSize -1] &= 0xFE;
       return new BigInteger(hash);
    }

/* Converts a string IP Address into a hostname */
    public static string DHCPGetHostname(string IPAddress) {
      string hostname = "C";
      byte [] IP = IPOP_Common.StringToBytes(IPAddress, '.');
      for(int i = 1; i < 4; i++) {
        if(IP[i] < 10)
          hostname += "00";
       else if(IP[i] < 100)
          hostname += "0";
        hostname += IP[i];
      }
      return hostname;
    }

    public static string GetMyGeoLoc() {
      try {
        string server = "wow.acis.ufl.edu";
        int port = 80;
        Regex lat = new Regex("Latitude.+");
        Regex lon = new Regex("Longitude.+");
        Regex num = new Regex("\\-{0,1}\\d+.\\d+");
        Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        s.Connect(server, port);
        string request = "GET /ip2geo/geo.php HTTP/1.1\r\nHost: wow.acis.ufl.edu\r\n\r\n";
        byte[] bs = Encoding.ASCII.GetBytes(request);
        s.Send(bs, bs.Length, 0);
        string page = String.Empty;
        byte[] br = new byte[256];
        int bytes = 0;
        do {
          bytes = s.Receive(br, br.Length, 0);
          page += Encoding.ASCII.GetString(br, 0, bytes);
        } while (bytes > 0);

        Match latm = lat.Match(page);
        Match lonm = lon.Match(page);
        if(latm.Success && lonm.Success) {
          latm = num.Match(latm.Value);
          lonm = num.Match(lonm.Value);
          if(latm.Success && lonm.Success) {
            return latm.Value + ", " + lonm.Value;
          }
        }
      }
      catch{}
      return ",";
    }

    public static byte [] StringToBytes(string input, char sep) {
      char [] separator = {sep};
      string[] ss = input.Split(separator);
      byte [] ret = new byte[ss.Length];
      for (int i = 0; i < ss.Length; i++) {
        ret[i] = byte.Parse(ss[i].Trim());
      }
      return ret;
    }

    public static byte [] HexStringToBytes(string input, char sep) {
      char [] separator = {sep};
      string[] ss = input.Split(separator);
      byte [] ret = new byte[ss.Length];
      for (int i = 0; i < ss.Length; i++) {
        ret[i] = byte.Parse(ss[i].Trim(), System.Globalization.NumberStyles.HexNumber);
      }
      return ret;
    }

    public static string BytesToString(byte [] input, char sep) {
      string return_msg = "";
      for(int i = 0; i < input.Length - 1; i++)
        return_msg += input[i].ToString() + sep.ToString();
      return_msg += input[input.Length - 1];
      return return_msg;
    }

    public static byte [] GenerateAddress() {
      AHAddress temp = GenerateAHAddress();
      byte [] tempb = new byte[20];
      temp.CopyTo(tempb);
      return tempb;
    }

    public static AHAddress GenerateAHAddress() {
      return new AHAddress(new RNGCryptoServiceProvider());
    }

    public static bool BrunetConnected(Node node) {
      RpcManager rpc = RpcManager.GetInstance(node);
      BlockingQueue queue = new BlockingQueue();
      byte[] target = Address.ConvertToAddressBuffer(
          node.Address.ToBigInteger() + Address.Half);
      Address.SetClass(target, AHAddress._class);
      AHSender s = new AHSender(rpc.Node, new AHAddress(target),
                                AHPacket.AHOptions.Greedy);
      rpc.Invoke(s, queue, "sys:link.Ping", "Test connection...");
      bool result = false;
      try {
        queue.Dequeue();
        result = true;
      }
      catch(Exception) {
        result = false;
      }
      Console.WriteLine("Connection result = " + result);
      return false;
    }
  }
}
