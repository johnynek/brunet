/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using Brunet;
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
    protected Dictionary<MemBlock, X509Certificate> _cas;
    /// <summary>List of supported CAs serial numbers.</summary>
    protected List<MemBlock> _supported_cas;
    /// <summary>Dictionary for local certificates by SerialNumber.</summary>
    protected Dictionary<MemBlock, X509Certificate> _lc;
    /// <summary>List of local certificate serial numbers.</summary>
    protected List<MemBlock> _lc_issuers;
    /// <summary>Default directory for storing certificates.</summary>
    public readonly String CertDir;
    protected object _sync;
    public bool Available {
      get {
        return _cas.Count > 0;
      }
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
    public List<MemBlock> SupportedCAs { get { return _supported_cas; } }

    public CertificateHandler() : this("certificates")
    {
    }

    public CertificateHandler(string cert_dir) {
      _sync = new object();
      lock(_sync) {
        CertDir = cert_dir;
        _cas = new Dictionary<MemBlock, X509Certificate>();
        _supported_cas = new List<MemBlock>();
        _lc = new Dictionary<MemBlock, X509Certificate>();
        _lc_issuers = new List<MemBlock>();
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
    public bool Verify(X509Certificate x509, string RemoteID) {
      if(!Verify(x509)) {
        throw new Exception("Invalid certificate!");
      }

      bool valid_address = false;
      foreach(X509Extension ex in x509.Extensions) {
        // SubjectAltName
        if(ex.Oid != "2.5.29.17") {
          continue;
        }
        SubjectAltNameExtension sane = new SubjectAltNameExtension(ex);
        foreach(string uri in sane.UniformResourceIdentifiers) {
          if(uri == RemoteID) {
            valid_address = true;
            break;
          }
        }
        if(valid_address) {
          break;
        }
      }
      if(!valid_address) {
        throw new Exception("Missing a valid SubjectAltName!");
      }

      return true;
    }

    /// <summary>True if this certificate is signed by a CA whose cetificate
    /// we have, false otherwise.</summary>
    public bool Verify(X509Certificate x509) {
      MemBlock sn = MemBlock.Reference(x509.SerialNumber);
      lock(_sync) {
        if(!_cas.ContainsKey(sn)) {
          throw new Exception("Unsupported CA!");
        }
        if(!x509.VerifySignature(_cas[sn].RSA)) {
          throw new Exception("Unable to verify certificate, bad signature!");
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
    public bool AddCACertificate(X509Certificate cert) {
      MemBlock sn = MemBlock.Reference(cert.SerialNumber);
      lock(_sync) {
        _cas[sn] = cert;
        _supported_cas.Add(sn);
      }
      return true;
    }

    /// <summary>Adds a local signed public certificate to this CH.</summary>
    public bool AddSignedCertificate(X509Certificate cert) {
      MemBlock sn = MemBlock.Reference(cert.SerialNumber);
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
    public X509Certificate FindCertificate(List<MemBlock> supported_cas) {
      lock(_sync) {
        foreach(MemBlock mem in supported_cas) {
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
      Assert.IsTrue(ch.Verify(cert_0.X509, ID), "Valid");
      bool success = false;
      try {
        success = ch.Verify(cert_1.X509, ID);
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

      List<MemBlock> supported = new List<MemBlock>();
      List<MemBlock> unsupported = new List<MemBlock>();

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

      List<MemBlock> mixed = new List<MemBlock>(unsupported);
      mixed.Insert(4 ,supported[1]);
      Assert.AreEqual(supported[1],
          MemBlock.Reference(ch.FindCertificate(mixed).SerialNumber),
          "Only one supported");
    }
  }
#endif
}
