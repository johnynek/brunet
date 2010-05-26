/*
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using Brunet.Security;
using Brunet.Util;
using Mono.Security.X509;
using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;

#if NUNIT
using NUnit.Framework;
#endif

namespace Ipop {
  /// <summary> A certificate handler for GroupVPN, provides the ability to revoke
  /// certificates by username alone rather then revoking individual certificates.</summary>
  public class GroupCertificateVerification : ICertificateVerification {
    public const int UPDATE_PERIOD = 24 * 60 * 60 * 1000;
    protected SimpleTimer _timer;
    protected string _revocation_url;
    protected Hashtable _revoked_users;
    protected Certificate _ca_cert;
    public delegate void RevocationUpdateHandler();
    public event RevocationUpdateHandler RevocationUpdate;
    

    public GroupCertificateVerification(string revocation_url, string cacert_path)
    {
      _revocation_url = revocation_url;
      _timer = new SimpleTimer(UpdateRl, null, 0, UPDATE_PERIOD);
      _timer.Start();
      _revoked_users = new Hashtable();

      using(FileStream fs = File.Open(cacert_path, FileMode.Open)) {
        byte[] cert = new byte[fs.Length];
        fs.Read(cert, 0, cert.Length);
        _ca_cert = new Certificate(cert);
      }
    }

    protected GroupCertificateVerification()
    {
    }

    /// <summary>Retrieves the latest user revocation list from the web and
    /// notifies that all SAs should be compared against the new revocation
    /// list.</summary>
    protected void UpdateRl(object o)
    {
      WaitCallback wcb = delegate(object obj) {
        try {
          byte[] data = DownloadList();
          UpdateRl(data);
          CheckSAs();
        } catch(Exception e) {
          ProtocolLog.WriteIf(IpopLog.GroupVPN, e.ToString());
        }
      };
      ThreadPool.QueueUserWorkItem(wcb);
    }

    /// <summary>Get the revocation list from the web.</summary>
    protected byte[] DownloadList()
    {
      WebClient wc = new WebClient();
      return wc.DownloadData(_revocation_url);
    }

    /// <summary>Parses web data and updates the revoked users hashtable if
    /// successful</summary>
    protected void UpdateRl(byte[] data)
    {
      // message is length (4) + date (8) + data (variable) + hash (~20)
      int length = data.Length;
      if(length < 12) {
        throw new Exception("No data?  Didn't get enough data...");
      }

      length = NumberSerializer.ReadInt(data, 0);
      DateTime date = new DateTime(NumberSerializer.ReadLong(data, 4));
      // warn the user that this is an old revocation list, maybe there is an attack
      if(date < DateTime.UtcNow.AddHours(-24)) {
        ProtocolLog.WriteIf(IpopLog.GroupVPN, "Revocation list is over 24 hours old");
      }

      // Weird, data length is longer than the data we got
      if(length > data.Length - 12) {
        throw new Exception("Missing data?  Didn't get enough data...");
      }

      // hash the data and verify the signature
      SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider();
      byte[] hash = sha1.ComputeHash(data, 4, length);
      byte[] signature = new byte[data.Length - 4 - length];
      Array.Copy(data, 4 + length, signature, 0, signature.Length);

      if(!_ca_cert.PublicKey.VerifyHash(hash, CryptoConfig.MapNameToOID("SHA1"), signature)) {
        throw new Exception("Invalid signature!");
      }

      // convert the data to an array list as it was sent to us
      MemBlock mem = MemBlock.Reference(data, 12, length - 8);

      ArrayList rl = AdrConverter.Deserialize(mem) as ArrayList;
      if(rl == null) {
        throw new Exception("Data wasn't a list...");
      }

      // convert it into a hashtable for O(1) look ups
      Hashtable ht = new Hashtable();
      foreach(string username in rl) {
        ht[username] = true;
      }

      Interlocked.Exchange(ref _revoked_users, ht);
    }

    /// <summary>Any listeners to RevocationUpdate will be notified that we have
    /// been updated</summary>
    protected void CheckSAs()
    {
      if(_revoked_users.Count > 0 && RevocationUpdate != null) {
        RevocationUpdate();
      }
    }

    /// <summary>True upon a non-revoked certificate, an exception otherwise.</summary>
    public bool Verify(X509Certificate x509, Brunet.Messaging.ISender sender)
    {
      Certificate cert = new Certificate(x509.RawData);
      if(!_revoked_users.Contains(cert.Subject.Name)) {
        return true;
      }
      throw new Exception("User has been revoked!");
    }
  }
#if NUNIT
  [TestFixture]
  public class GroupCertificateVerificationTest : GroupCertificateVerification {
    RSACryptoServiceProvider _private_key;
    const string _remote_id = "0";

    public GroupCertificateVerificationTest()
    {
      _private_key = new RSACryptoServiceProvider(512);
      byte[] blob = _private_key.ExportCspBlob(false);
      RSACryptoServiceProvider rsa_pub = new RSACryptoServiceProvider();
      rsa_pub.ImportCspBlob(blob);
      CertificateMaker cm = new CertificateMaker("US", "UFL", "ACIS", "David Wolinsky",
          "davidiw@ufl.edu", rsa_pub, "davidiw");
      _ca_cert = cm.Sign(cm, _private_key);
    }

    protected X509Certificate CreateCert(string username)
    {
      RSACryptoServiceProvider pub = new RSACryptoServiceProvider();
      CertificateMaker cm = new CertificateMaker("US", "UFL", "ACIS", username,
          "davidiw@ufl.edu", pub, _remote_id);
      return cm.Sign(_ca_cert, _private_key).X509;
    }

    [Test]
    public void Test() {
      CertificateHandler ch = new CertificateHandler();
      ch.AddCACertificate(_ca_cert.X509);
      ch.AddCertificateVerification(this);

      ArrayList revoked_users = new ArrayList();
      revoked_users.Add("joker");
      revoked_users.Add("bad_guy");
      revoked_users.Add("adversary");
      revoked_users.Add("noobs");

      // create revocation list
      byte[] to_sign = null;
      using(MemoryStream ms = new MemoryStream()) {
        NumberSerializer.WriteLong(DateTime.UtcNow.Ticks, ms);
        AdrConverter.Serialize(revoked_users, ms); to_sign = ms.ToArray();
      }

      // sign revocation list
      SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider();
      byte[] hash = sha1.ComputeHash(to_sign);
      byte[] signature = _private_key.SignHash(hash, CryptoConfig.MapNameToOID("SHA1"));
      byte[] data = new byte[4 + to_sign.Length + signature.Length];
      NumberSerializer.WriteInt(to_sign.Length, data, 0);
      to_sign.CopyTo(data, 4);
      signature.CopyTo(data, 4 + to_sign.Length);
 
      UpdateRl(data);

      X509Certificate likable_guy = CreateCert("likable_guy");
      X509Certificate joker = CreateCert("joker");
      X509Certificate bad_guy = CreateCert("bad_guy");
      X509Certificate good_guy = CreateCert("good_guy");
      X509Certificate adversary = CreateCert("adversary");
      X509Certificate noobs =  CreateCert("noobs");
      X509Certificate friendly_guy =  CreateCert("friendly_guy");

      Assert.IsTrue(ch.Verify(likable_guy, null, _remote_id), "Likable guy");
      bool success = false;
      try {
        success = ch.Verify(adversary, null, _remote_id);
      } catch { }
      Assert.AreEqual(success, false, "adversary");

      try {
        success = ch.Verify(joker, null, _remote_id);
      } catch { }
      Assert.AreEqual(success, false, "joker");

      Assert.IsTrue(ch.Verify(friendly_guy, null, _remote_id), "friendly guy");

      try {
        success = ch.Verify(noobs, null, _remote_id);
      } catch { }
      Assert.AreEqual(success, false, "noobs");

      try {
        success = ch.Verify(bad_guy, null, _remote_id);
      } catch { }
      Assert.AreEqual(success, false, "bad_guy");

      Assert.IsTrue(ch.Verify(good_guy, null, _remote_id), "good guy");
    }
  }
#endif
}
