/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using Brunet;
using Brunet.Util;
using Mono.Security.X509;
using Mono.Security.X509.Extensions;
using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;

#if BRUNET_NUNIT
using NUnit.Framework;
using Mono.Math;
#endif

namespace Brunet.Security {
  /// <summary>This represents an X509 certificate in a more usable format
  /// for our system.</summary>
  public class Certificate {
    public DistinguishedName Issuer { get { return _issuer; } }
    protected DistinguishedName _issuer;
    public DistinguishedName Subject { get { return _subject; } }
    protected DistinguishedName _subject;
    public byte[] Signature { get { return _signature; } }
    protected byte[] _signature;
    public byte[] SerialNumber { get { return _serial_number; } }
    protected byte[] _serial_number;

    /// <summary>This is the NodeAddress in the SubjectAltName</summary>
    public string NodeAddress { get { return _node_address; } }
    protected string _node_address;
    public RSACryptoServiceProvider PublicKey { get { return _public_key; } }
    protected RSACryptoServiceProvider _public_key;
    public X509Certificate X509 { get { return _x509; } }
    protected X509Certificate _x509;

    protected byte[] _unsigned_data;
    /// <summary>This represents the data in an unsigned certificate.</summary>
    public virtual byte[] UnsignedData {
      get {
        if(_unsigned_data == null) {
          Hashtable ht = new Hashtable();
          ht["Country"] = _subject.Country;
          ht["Organization"] = _subject.Organization;
          ht["OrganizationalUnit"] = _subject.OrganizationalUnit;
          ht["Name"] = _subject.Name;
          ht["Email"] = _subject.Email;
          byte[] blob = _public_key.ExportCspBlob(false);
          ht["PublicKey"] = blob;
          ht["NodeAddress"] = _node_address;
          using(MemoryStream ms = new MemoryStream()) {
            AdrConverter.Serialize(ht, ms);
            _unsigned_data = ms.ToArray();
          }
        }
        return _unsigned_data;
      }
    }

    protected Certificate()
    {
    }

    public Certificate(byte[] CertData): this(new X509Certificate(CertData))
    {
    }

    public Certificate(X509Certificate Cert)
    {
      _x509 = Cert;
      _issuer = new DistinguishedName(Cert.IssuerName);
      _subject = new DistinguishedName(Cert.SubjectName);
      _signature = Cert.Signature;
      _serial_number = Cert.SerialNumber;
      _public_key = (RSACryptoServiceProvider) Cert.RSA;
      SubjectAltNameExtension sane = new SubjectAltNameExtension(Cert.Extensions[0]);
      _node_address = sane.UniformResourceIdentifiers[0];
    }

    public Certificate(DistinguishedName Issuer, DistinguishedName Subject,
        byte[] Signature, byte[] SerialNumber, string NodeAddress,
        RSACryptoServiceProvider PublicKey)
    {
      _issuer = Issuer;
      _subject = Subject;
      _signature = Signature;
      _serial_number = SerialNumber;
      _node_address = NodeAddress;
      _public_key = PublicKey;
    }

    public override string ToString() {
      StringBuilder sb = new StringBuilder();
      sb.Append(this.GetType());
      sb.Append("\nSubject: ");
      sb.Append(_subject.DN);
      if(_issuer != null) {
        sb.Append("\nIssuer: ");
        sb.Append(_issuer.DN);
      }
      sb.Append("\nSigned: ");
      sb.Append(_signature != null);
      if(_serial_number != null) {
        sb.Append("\nSerialNumber: ");
        sb.Append(new BigInteger(_serial_number));
      }
      if(_node_address != null) {
        sb.Append("\nNodeAddress: ");
        sb.Append(_node_address);
      }
      return sb.ToString();
    }
  }

  /// <summary>This class is used to make signing certificates easier.  The
  /// other method is to use the OpenSSL stack, which this has been verified
  /// to work with.</summary>
  public class CertificateMaker: Certificate {
    public CertificateMaker(string Country, string Organization,
        string OrganizationalUnit, string Name, string Email,
        RSACryptoServiceProvider PublicKey, string NodeAddress)
    {
      _subject = new DistinguishedName(Country, Organization,
          OrganizationalUnit, Name, Email);
      _public_key = PublicKey;
      _node_address = NodeAddress;
    }

    public CertificateMaker(byte[] RawData) {
      _unsigned_data = RawData;
      Hashtable ht = (Hashtable) AdrConverter.Deserialize(MemBlock.Reference(RawData));
      _subject = new DistinguishedName((string) ht["Country"],
          (string) ht["Organization"], (string) ht["OrganizationalUnit"],
          (string) ht["Name"], (string) ht["Email"]);
      RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
      rsa.ImportCspBlob((byte[]) ht["PublicKey"]);
      _public_key = rsa;
      _node_address = (string) ht["NodeAddress"];
    }

    public CertificateMaker(X509Certificate x509)
    {
      _subject = new DistinguishedName(x509.SubjectName);
      _public_key = (RSACryptoServiceProvider) x509.RSA;
      SubjectAltNameExtension sane = new SubjectAltNameExtension(x509.Extensions[0]);
      _node_address = sane.UniformResourceIdentifiers[0];
    }

    /// <summary>This method is called by a CA to sign the provided Certificate
    /// with our RSA key.</summary>
    public Certificate Sign(Certificate Signer, RSA PrivateKey)
    {
      X509CertificateBuilder x509_builder = new X509CertificateBuilder(3);
      x509_builder.IssuerName = Signer.Subject.DN;
      x509_builder.SubjectName = Subject.DN;
      SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider();
      // I guess this is reversed for network order or something...
      byte[] tmp = sha1.ComputeHash(Signer.UnsignedData);
      for(int i = 0; i < tmp.Length / 2; i++) {
        int j = tmp.Length - i - 1;
        byte tmpb = tmp[i];
        tmp[i] = tmp[j];
        tmp[j] = tmpb;
      }
      x509_builder.SerialNumber = tmp;
      x509_builder.NotBefore = System.DateTime.MinValue;
      x509_builder.NotAfter = System.DateTime.MaxValue;
      x509_builder.SubjectPublicKey = _public_key;
      SubjectAltNameExtension sane = new SubjectAltNameExtension(null, null, null, new string[] {NodeAddress});
      x509_builder.Extensions.Add(sane);
      byte[] cert_data = x509_builder.Sign(PrivateKey);
      return new Certificate(cert_data);
    }
  }

  /// <summary>This provide a parsing wrapper around Distinguished names.</summary>
  public class DistinguishedName {
    public readonly string Country;
    public readonly string Organization;
    public readonly string OrganizationalUnit;
    public readonly string Name;
    public readonly string Email;
    public readonly string DN;

    public DistinguishedName(string Country, string Organization,
        string OrganizationalUnit, string Name, string Email)
    {
      this.Country = Country;
      this.Organization = Organization;
      this.OrganizationalUnit = OrganizationalUnit;
      this.Name = Name;
      this.Email = Email;
      DN = BuildDN();
    }

    public DistinguishedName(string DN) {
      string[] pairs = DN.Split(',');
      foreach(string pair in pairs) {
        string[] key_value = pair.Split('=');
        string key = key_value[0].Trim(' ');
        string val = key_value[1].Trim(' ');
        switch(key) {
          case "C":
            Country = val;
            break;
          case "O":
            Organization = val;
            break;
          case "OU":
            OrganizationalUnit = val;
            break;
          case "CN":
            Name = val;
            break;
          case "E":
            Email = val;
            break;
        }
      }

      this.DN = BuildDN();
    }

    protected string BuildDN() {
      StringBuilder sb = new StringBuilder();
      sb.Append("C=");
      sb.Append(Country);
      sb.Append(", O=");
      sb.Append(Organization);
      sb.Append(", OU=");
      sb.Append(OrganizationalUnit);
      sb.Append(", CN=");
      sb.Append(Name);
      sb.Append(", E=");
      sb.Append(Email);
      return sb.ToString();
    }
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class CertificateTester {
    [Test]
    public void Test() {
      RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
      byte[] blob = rsa.ExportCspBlob(false);
      RSACryptoServiceProvider rsa_pub = new RSACryptoServiceProvider();
      rsa_pub.ImportCspBlob(blob);
      CertificateMaker cm = new CertificateMaker("United States", "UFL", 
          "ACIS", "David Wolinsky", "davidiw@ufl.edu", rsa_pub,
          "brunet:node:abcdefghijklmnopqrs");
      Assert.AreEqual("C=United States, O=UFL, OU=ACIS, CN=David Wolinsky, E=davidiw@ufl.edu", cm.Subject.DN, "DN test 1");
      cm = new CertificateMaker(cm.UnsignedData);
      Assert.AreEqual("C=United States, O=UFL, OU=ACIS, CN=David Wolinsky, E=davidiw@ufl.edu", cm.Subject.DN, "DN test 2");

      Certificate cert = cm.Sign(cm, rsa);

      Assert.IsTrue(cert.Signature != null, "Signature");
      Assert.AreEqual(cm.Subject.DN, cert.Issuer.DN, "Issuer = Subject");
      Assert.AreEqual("brunet:node:abcdefghijklmnopqrs", cert.NodeAddress , "Node address");

      Mono.Math.BigInteger rsa_pub_bi = new Mono.Math.BigInteger(rsa_pub.ExportCspBlob(false));
      Mono.Math.BigInteger cert_pub_bi = new Mono.Math.BigInteger(cert.PublicKey.ExportCspBlob(false));
      Assert.AreEqual(rsa_pub_bi, cert_pub_bi, "Key");

      SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider();
      Assert.AreEqual(MemBlock.Reference(cert.SerialNumber),
          MemBlock.Reference(sha1.ComputeHash(cert.UnsignedData)),
          "SerialNumber == hash of unsigned data");

    }
  }
#endif
}
