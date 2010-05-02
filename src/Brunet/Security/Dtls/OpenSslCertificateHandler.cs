using Brunet;
using mx509 = Mono.Security.X509;
using Mono.Security.X509.Extensions;
using OpenSSL.Core;
using ox509 = OpenSSL.X509;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Brunet.Util;

#if BRUNET_NUNIT
using NUnit.Framework;
using Mono.Math;
#endif

namespace Brunet.Security.Dtls {

  public class OpenSslCertificateHandler : CertificateHandler {
    public readonly ox509.X509Store Store;
    public ox509.X509Certificate LocalCertificate {
      get {
        return _local_certificate;
      }
    }
    protected ox509.X509Certificate _local_certificate;

    public OpenSslCertificateHandler() : this("certificates")
    {
    }

    public OpenSslCertificateHandler(string cert_dir) : base(cert_dir)
    {
      Store = new ox509.X509Store();
    }

    override public bool AddCACertificate(mx509.X509Certificate cert)
    {
      if(!base.AddCACertificate(cert)) {
        return false;
      }

      Store.AddTrusted(MonoX509ToOpenSsl(cert));
      return true;
    }

    override public bool AddSignedCertificate(mx509.X509Certificate cert)
    {
      if(!base.AddSignedCertificate(cert)) {
        return false;
      }

      _local_certificate = MonoX509ToOpenSsl(cert);
      return true;
    }

    public static mx509.X509Certificate OpenSslX509ToMonoX509(ox509.X509Certificate cert)
    {
      BIO bio = BIO.MemoryBuffer(true);
      cert.Write(bio);
      byte[] raw = new byte[bio.BytesPending];
      bio.Read(raw, raw.Length);
      bio.Dispose();
      return new mx509.X509Certificate(raw);
    }

    public static ox509.X509Certificate MonoX509ToOpenSsl(mx509.X509Certificate cert)
    {
      BIO bio = BIO.MemoryBuffer(true);
      bio.Write(cert.RawData);
      var ocert = ox509.X509Certificate.FromDER(bio);
      bio.Dispose();
      return ocert;
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class OchUnitTest {
    [Test]
    public void ValidityTest() {
      var osch = new OpenSslCertificateHandler();
      RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(512);
      byte[] blob = rsa.ExportCspBlob(false);
      RSACryptoServiceProvider rsa_pub = new RSACryptoServiceProvider();
      rsa_pub.ImportCspBlob(blob);
      string ID = "brunet:node:PXYSWDL5SZDHDDXJKZCLFENOP2KZDMBU";
      CertificateMaker cm = new CertificateMaker("US", "UFL", "ACIS", "David Wolinsky",
          "davidiw@ufl.edu", rsa_pub, ID);
      Certificate cert_0 = cm.Sign(cm, rsa);
      osch.AddSignedCertificate(cert_0.X509);
      osch.AddCACertificate(cert_0.X509);
      var ocert = OpenSslCertificateHandler.OpenSslX509ToMonoX509(osch.LocalCertificate);
      Assert.AreEqual(cert_0.X509.RawData, ocert.RawData, "local check");
      Assert.IsTrue(osch.Verify(ocert, ID), "Valid");
    }
  }
#endif
}
