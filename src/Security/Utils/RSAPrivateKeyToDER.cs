using Mono.Security;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System;

/// Makes a PKCS8 compatible file for the inputed private file
public class RSAPrivateKeyToDER {
  ///
  /// SEQUENCE (a)
  ///  +- INTEGER (V)              // Version - 0 (v1998)
  ///  +- SEQUENCE (b)
  ///  |   +- OID (oid)            // 1.2.840.113549.1.1.1
  ///  |   +- Nil (c)
  ///  +- OCTETSTRING(PRVKY) (os)  // Private Key Parameter
  ///
  ///  However, OCTETSTRING(PRVKY) wraps
  ///    SEQUENCE(
  ///      INTEGER(0)              // Version - 0 (v1998)
  ///      INTEGER(N)
  ///      INTEGER(E)
  ///      INTEGER(D)
  ///      INTEGER(P)
  ///      INTEGER(Q)
  ///      INTEGER(DP)
  ///      INTEGER(DQ)
  ///      INTEGER(InvQ)
  ///    )
  public static byte[] RSAKeyToASN1(RSAParameters PrivateKey) {
    ASN1 v = ASN1Convert.FromUnsignedBigInteger(new byte[] {0});

    ASN1 b = PKCS7.AlgorithmIdentifier ("1.2.840.113549.1.1.1");

    ASN1 os = new ASN1(0x30);
    os.Add(ASN1Convert.FromUnsignedBigInteger(new byte[] {0}));
    os.Add(ASN1Convert.FromUnsignedBigInteger(PrivateKey.Modulus));
    os.Add(ASN1Convert.FromUnsignedBigInteger(PrivateKey.Exponent));
    os.Add(ASN1Convert.FromUnsignedBigInteger(PrivateKey.D));
    os.Add(ASN1Convert.FromUnsignedBigInteger(PrivateKey.P));
    os.Add(ASN1Convert.FromUnsignedBigInteger(PrivateKey.Q));
    os.Add(ASN1Convert.FromUnsignedBigInteger(PrivateKey.DP));
    os.Add(ASN1Convert.FromUnsignedBigInteger(PrivateKey.DQ));
    os.Add(ASN1Convert.FromUnsignedBigInteger(PrivateKey.InverseQ));

    ASN1 pem = new ASN1(0x30);
    pem.Add(v);
    pem.Add(b);
    // Make this into an OCTET string
    pem.Add(new ASN1(0x04, os.GetBytes()));
    return pem.GetBytes();
  }

  public static void Main(string[] args) {
    string path = args[0];
    byte[] blob = null;
    using(FileStream fs = File.Open(path, FileMode.Open)) {
      blob = new byte[fs.Length];
      fs.Read(blob, 0, blob.Length);
    }

    RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
    rsa.ImportCspBlob(blob);
    RSAParameters PrivateKey = rsa.ExportParameters(true);
    byte[] key = RSAKeyToASN1(PrivateKey);

    using(FileStream fs = File.Open(path + ".out", FileMode.Create)) {
      fs.Write(key, 0, key.Length);
    }

    Console.WriteLine("Your file is ready for you at " + path + ".out.");
  }
}
