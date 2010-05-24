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
using Brunet.Messaging;
using Brunet.Util;
using Mono.Security.X509;
using Mono.Security.X509.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
#if BRUNET_NUNIT
using NUnit.Framework;
using Mono.Math;
#endif

namespace Brunet.Security {
  /// <summary>.NET provides a very complicated (and complete) x509 Certificate
  /// handler.  We do not require all those features and we also embed data that
  /// we use for quick retrieval that wouldn't be easy to reproduce using that
  /// framework.  This model can be used on non-x509 certificate models and
  /// perhaps even abstract to support handshakes on pre-shared keys.
  /// In our system, certificate's serial numbers are equivalent to the
  /// data common to a certificate request and signed certificate, so that the
  /// model can support self-signed CAs.  Thus a cert.SerialNumber == 
  /// hash(cert.ca.unsigned data).  This class is thread-safe.</summary>
  public class CertificateHandler {
    /// <summary>Dictionary for authorities local certificates by SerialNumber.</summary>
    protected Dictionary<Brunet.Util.MemBlock, X509Certificate> _cas;
    /// <summary>List of supported CAs serial numbers.</summary>
    protected List<Brunet.Util.MemBlock> _supported_cas;
    /// <summary>Dictionary for local certificates by SerialNumber.</summary>
    protected Dictionary<Brunet.Util.MemBlock, X509Certificate> _lc;
    /// <summary>List of local certificate serial numbers.</summary>
    protected List<Brunet.Util.MemBlock> _lc_issuers;
    /// <summary>Default directory for storing certificates.</summary>
    public readonly String CertDir;
    protected object _sync;
    protected readonly string _local_id;
    public bool Available {
      get {
        return _cas.Count > 0;
      }
    }

    protected List<ICertificateVerification> _certificate_verifiers;
    /// <summary>Add an ICertificateVerification to be called during verification of
    /// certificates</summary>
    public bool AddCertificateVerification(ICertificateVerification certificate_verifier) {
      _certificate_verifiers.Add(certificate_verifier);
      return true;
    }

    /// <summary>Remove an  ICertificateVerification.</summary>
    public bool RemoveCertificateVerification(ICertificateVerification certificate_verifier) {
      return _certificate_verifiers.Remove(certificate_verifier);
    }

    // The default certificate is the first of our certificates that are entered
    // into this system
    public X509Certificate DefaultCertificate { 
      get {
        X509Certificate cert = null;
        foreach(X509Certificate x509 in _lc.Values) {
          cert = x509;
          break;
        }
        return cert;
      }
    }


    /// <summary>List of supported CAs serial numbers.</summary>
    public List<Brunet.Util.MemBlock> SupportedCAs { get { return _supported_cas; } }

    public CertificateHandler() : this("certificates", String.Empty)
    {
    }

    public CertificateHandler(string cert_dir) : this(cert_dir, String.Empty)
    {
    }

    public CertificateHandler(string cert_dir, string local_id) {
      _sync = new object();
      lock(_sync) {
        _local_id = local_id;
        _certificate_verifiers = new List<ICertificateVerification>();
        CertDir = cert_dir;
        _cas = new Dictionary<Brunet.Util.MemBlock, X509Certificate>();
        _supported_cas = new List<Brunet.Util.MemBlock>();
        _lc = new Dictionary<Brunet.Util.MemBlock, X509Certificate>();
        _lc_issuers = new List<Brunet.Util.MemBlock>();
        LoadAllCertificates();
      }
    }

    /// <summary>First makes sure we have a CA that supports this certificate,
    /// then looks through an x509 certificates SubjectAltName Extension's
    /// URI list to determine if the given URI (RemoteID) exists in the
    /// certificate.</summary>
    /// <param name="x509">The certificate to check</param>
    /// <param name="RemoteID">The URI to look for</param>
    /// <returns>True if the URI exists, false otherwise</returns>
    virtual public bool Verify(X509Certificate x509, ISender sender,
        string remote_id)
    {
      if(Verify(x509, sender) && Verify(x509, remote_id)) {
        return true;
      }
      throw new Exception("Invalid certificate.");
    }

    ///<summary>Given a string, this looks inside the certificates SANE to see
    ///if the string is present.  This isn't inefficient as it looks, there
    ///tends to be no entries at most of those places, so this usually has
    ///runtime of 1.</summary>
    virtual public bool Verify(X509Certificate x509, string remote_id) {
      foreach(X509Extension ext in x509.Extensions) {
        if(!ext.Oid.Equals("2.5.29.17")) {
          continue;
        }
        SubjectAltNameExtension sane = new SubjectAltNameExtension(ext);
        foreach(string name in sane.RFC822) {
          if(name.Equals(remote_id)) {
            return true;
          }
        }
        foreach(string name in sane.DNSNames) {
          if(name.Equals(remote_id)) {
            return true;
          }
        }
        foreach(string name in sane.IPAddresses) {
          if(name.Equals(remote_id)) {
            return true;
          }
        }
        foreach(string name in sane.UniformResourceIdentifiers) {
          if(name.Equals(remote_id)) {
            return true;
          }
        }
      }
      throw new Exception("Missing a valid SubjectAltName!");
    }

    /// <summary>True if this certificate is signed by a CA whose cetificate
    /// we have, false otherwise.</summary>
    public bool Verify(X509Certificate x509, ISender sender) {
      Brunet.Util.MemBlock sn = Brunet.Util.MemBlock.Reference(x509.SerialNumber);
      lock(_sync) {
        if(!_cas.ContainsKey(sn)) {
          throw new Exception("Unsupported CA!");
        }
        if(!x509.VerifySignature(_cas[sn].RSA)) {
          throw new Exception("Unable to verify certificate, bad signature!");
        }
      }

      foreach(ICertificateVerification icv in _certificate_verifiers) {
        if(!icv.Verify(x509, sender)) {
          throw new Exception("Certificate not valid, reason unsure");
        }
      }

      return true;
    }

    /// <summary>Loads all the local certificates.</summary>
    public void LoadAllCertificates() {
      String[] files = null;
      try {
        files = Directory.GetFiles(CertDir);
      } catch {
        return;
      }

      foreach(String file in files) {
        try {
          String filename = file.Substring(CertDir.Length + 1);
          AddCertificate(filename);
        }
        catch (Exception e) {
          ProtocolLog.WriteIf(ProtocolLog.Security, e.ToString());
        }
      }
    }

    /// <summary>Reads a certificate from a file.</summary>
    public X509Certificate ReadCertificate(string Filename) {
      String path = Path.Combine(CertDir, Filename);
      FileStream file = File.Open(path, FileMode.Open);
      byte[] cert_blob = new byte[file.Length];
      file.Read(cert_blob, 0, (int) file.Length);
      file.Close();

      return new X509Certificate(cert_blob);
    }

    public bool AddCertificate(string filename) {
      X509Certificate x509 = ReadCertificate(filename);
      if(filename.StartsWith("ca")) {
        return AddCACertificate(x509);
      } else if(filename.StartsWith("lc")) {
        return AddSignedCertificate(x509);
      } else {
        /// @todo add ability to cache certificates.
      }
      return false;
    }

    /// <summary>Adds CA certificate to this CH.</summary>
    virtual public bool AddCACertificate(X509Certificate cert) {
      Brunet.Util.MemBlock sn = Brunet.Util.MemBlock.Reference(cert.SerialNumber);
      lock(_sync) {
        _cas[sn] = cert;
        _supported_cas.Add(sn);
      }
      return true;
    }

    /// <summary>Adds a local signed public certificate to this CH.</summary>
    virtual public bool AddSignedCertificate(X509Certificate cert) {
      if(_local_id != String.Empty && !Verify(cert, _local_id)) {
        throw new Exception("Invalid certificate: " + cert);
      }

      Brunet.Util.MemBlock sn = Brunet.Util.MemBlock.Reference(cert.SerialNumber);
      lock(_sync) {
        _lc[sn] = cert;
        _lc_issuers.Add(sn);
      }
      return true;
    }

    /// <summary>Returns the first certificate that matches one of the CAs
    /// listed in the array.</summary>
    /// <param name="supported_cas">A list of CAs of which you would like to
    /// find a certificate that matches.</param>
    /// <returns>A certificate that is supported by one of the CAs in the list
    /// </returns>
    public X509Certificate FindCertificate(List<Brunet.Util.MemBlock> supported_cas) {
      lock(_sync) {
        foreach(Brunet.Util.MemBlock mem in supported_cas) {
          if(_lc_issuers.Contains(mem)) {
            return _lc[mem];
          }
        }
      }
      throw new Exception("No supported certificate found!");
    }
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class CHUnitTest {
    [Test]
    public void AddBadLocalCert() {
      CertificateHandler ch = new CertificateHandler("certs", "12345");
      RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(512);
      byte[] blob = rsa.ExportCspBlob(false);
      RSACryptoServiceProvider rsa_pub = new RSACryptoServiceProvider();
      rsa_pub.ImportCspBlob(blob);
      string ID = "brunet:node:PXYSWDL5SZDHDDXJKZCLFENOP2KZDMBU";
      CertificateMaker cm = new CertificateMaker("US", "UFL", "ACIS", "David Wolinsky",
          "davidiw@ufl.edu", rsa_pub, ID);
      Certificate cert_0 = cm.Sign(cm, rsa);
      ch.AddCACertificate(cert_0.X509);
      try {
        ch.AddSignedCertificate(cert_0.X509);
        Assert.IsTrue(false, "Shouldn't add this certificate!");
      } catch {
      }

      CertificateMaker cm0 = new CertificateMaker("US", "UFL", "ACIS", "David Wolinsky",
          "davidiw@ufl.edu", rsa_pub, "12345");
      Certificate cert_1 = cm0.Sign(cm, rsa);
      ch.AddSignedCertificate(cert_1.X509);
    }

    [Test]
    public void ValidityTest() {
      CertificateHandler ch = new CertificateHandler();
      RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(512);
      byte[] blob = rsa.ExportCspBlob(false);
      RSACryptoServiceProvider rsa_pub = new RSACryptoServiceProvider();
      rsa_pub.ImportCspBlob(blob);
      string ID = "brunet:node:PXYSWDL5SZDHDDXJKZCLFENOP2KZDMBU";
      CertificateMaker cm = new CertificateMaker("US", "UFL", "ACIS", "David Wolinsky",
          "davidiw@ufl.edu", rsa_pub, ID);
      Certificate cert_0 = cm.Sign(cm, rsa);
      ch.AddSignedCertificate(cert_0.X509);
      ch.AddCACertificate(cert_0.X509);
      rsa = new RSACryptoServiceProvider(1024);
      rsa_pub.ImportCspBlob(rsa.ExportCspBlob(false));
      cm = new CertificateMaker("US", "UFL", "ACIS", "David Wolinsky",
          "davidiw@ufl.edu", rsa_pub, ID);
      Certificate cert_1 = cm.Sign(cm, rsa);
      Assert.IsTrue(ch.Verify(cert_0.X509, null, ID), "Valid");
      bool success = false;
      try {
        success = ch.Verify(cert_1.X509, null, ID);
      } catch { }
      Assert.IsTrue(!success, "Valid cert2");
    }

    [Test]
    public void FindCertificateTest() {
      CertificateHandler ch = new CertificateHandler();

      RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(512);
      byte[] blob = rsa.ExportCspBlob(false);
      RSACryptoServiceProvider rsa_pub = new RSACryptoServiceProvider();
      rsa_pub.ImportCspBlob(blob);

      List<Brunet.Util.MemBlock> supported = new List<Brunet.Util.MemBlock>();
      List<Brunet.Util.MemBlock> unsupported = new List<Brunet.Util.MemBlock>();

      for(int i = 0; i < 20; i++) {
        CertificateMaker cm = new CertificateMaker("US", "UFL", "ACIS", "David Wolinsky",
            "davidiw@ufl.edu" + i, rsa_pub, i.ToString());
        Certificate cert = cm.Sign(cm, rsa);
        if(i % 2 == 0) {
          ch.AddCACertificate(cert.X509);
          ch.AddSignedCertificate(cert.X509);
          supported.Add(cert.SerialNumber);
        } else {
          unsupported.Add(cert.SerialNumber);
        }
      }

      Assert.IsNotNull(ch.FindCertificate(supported), "Should find a certificate");

      bool success = false;
      try {
        success = ch.FindCertificate(unsupported) != null;
      } catch { }

      Assert.IsTrue(!success, "Should not find a certificate");

      List<Brunet.Util.MemBlock> mixed = new List<Brunet.Util.MemBlock>(unsupported);
      mixed.Insert(4 ,supported[1]);
      Assert.AreEqual(supported[1],
          Brunet.Util.MemBlock.Reference(ch.FindCertificate(mixed).SerialNumber),
          "Only one supported");
    }
  }
#endif
}
