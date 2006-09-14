using System;
using System.Text;
namespace Ipop {
  public class IPAddress {
    public static readonly int IP_ADDR_LEN = 4;
    private byte[] addr;
    public byte[] IPBuffer  {
      get {
	return addr;
      }
    }
    //IP adress is kept as a byte array of 4 bytes
    public IPAddress(byte[] b) {
      addr = new byte[IP_ADDR_LEN];
      Array.Copy(b, addr, IP_ADDR_LEN);
    }
    //create from string
    public IPAddress(string ip) {
      char [] separator = {'.'};
      string[] ss = ip.Split(separator, IP_ADDR_LEN);
      addr = new byte[IP_ADDR_LEN];
      for (int i = 0; i < IP_ADDR_LEN; i++) {
	addr[i] = Byte.Parse(ss[i].Trim());
      }
    }
    public override bool Equals(Object param) {
      if (param.GetType() != typeof(IPAddress)) {
	return false;
      }
      IPAddress other = (IPAddress) param;
      for (int i = 0; i < IP_ADDR_LEN; i++) {
	if (addr[i] != other.IPBuffer[i]) {
	  //Console.WriteLine(addr[i] + " : " + other.IPBuffer[i]);
	  return false;
	}
      }
      return true;
    }
    public bool InSubnet(IPAddress other) {
      for (int i = 0; i < IP_ADDR_LEN - 1; i++) {
	if (addr[i] != other.IPBuffer[i]) {
	  //Console.WriteLine(addr[i] + " : " + other.IPBuffer[i]);
	  return false;
	} 
      }
      return true;
    }
    public override int GetHashCode() {
      int hash = 0;
      for (int i = 0; i < IP_ADDR_LEN; i++) {
	hash = (hash << 4) + addr[i];
      }
      return hash;
    }

    public override string ToString() {
      StringBuilder sb = new StringBuilder();
      for (int i = 0; i < IP_ADDR_LEN; i++) {
	sb.Append(addr[i]);
	if (i < IP_ADDR_LEN -1) {
	  sb.Append(".");
	}
      }
      return sb.ToString();
    }
    /*public static void Main(string[] args) {
    }*/
  }
}
