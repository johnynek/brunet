using Brunet;
using System.Net;
using System.Security.Cryptography;

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
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte [] temp = new byte[16];
      rng.GetBytes(temp);
      return temp;
    }
  }
}