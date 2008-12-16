using System;
using System.Security.Cryptography;

namespace Brunet.Security {
  public class AddressGenerator {
    public static void Main() {
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      AHAddress addr = new AHAddress(rng);
      Console.WriteLine(addr.ToString());
    }
  }
}
