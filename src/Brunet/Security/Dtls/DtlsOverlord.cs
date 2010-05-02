using Brunet.Messaging;
using Brunet.Security.Utils;
using Brunet.Util;
using OpenSSL;
using OpenSSL.Core;
using OpenSSL.Crypto;
using OpenSSL.X509;
using System;
using System.Threading;
using System.Security.Cryptography;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

#if BRUNET_NUNIT
using Brunet.Messaging.Mock;
using mx509 = Mono.Security.X509;
using NUnit.Framework;
#endif

namespace Brunet.Security.Dtls {
  /// <summary></summary>
  public class DtlsOverlord : SecurityOverlord {
    protected class IdentifierTableAsDtlsAssociation :
      IEnumerable<SecurityAssociation>
    {
      IdentifierTable _it;
    
      public IdentifierTableAsDtlsAssociation(IdentifierTable it)
      {
        _it = it; 
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
        return GetEnumerator();
      }
    
      public IEnumerator<SecurityAssociation> GetEnumerator()
      {
        foreach(DtlsAssociation da in _it) {
          yield return da;
        }
      }
    }

    /// <summary></summary>
    public readonly PType PType;
    override public int SACount { get { return _it.Count; } }

    protected readonly IdentifierTable _it;
    protected readonly SslContext _ctx;
    protected readonly OpenSslCertificateHandler _osch;
    protected readonly MemBlock _ptype_mb;
    protected readonly ReaderWriterLock _rwl;
    override protected IEnumerable<SecurityAssociation> _sas { get { return _sas_helper; } }
    protected IEnumerable<SecurityAssociation> _sas_helper;
    protected readonly Dictionary<ISender, DtlsAssociation> _sender_to_sa;
    protected MemBlock _cookie;

    /// <summary></summary>
    public DtlsOverlord(RSACryptoServiceProvider private_key,
        CertificateHandler ch, PType ptype) : base(private_key, ch)
    {
      _osch = ch as OpenSslCertificateHandler;
      if(_osch == null) {
        throw new Exception("CertificateHandler is invalid type: " + ch.GetType());
      }

      _it = new IdentifierTable();
      _sas_helper = new IdentifierTableAsDtlsAssociation(_it);
      _rwl = new ReaderWriterLock();
      _sender_to_sa = new Dictionary<ISender, DtlsAssociation>();

      PType = ptype;
      _ptype_mb = ptype.ToMemBlock();

      _ctx = new SslContext(SslMethod.DTLSv1_method);
      _ctx.SetCertificateStore(_osch.Store);
      _ctx.SetVerify(VerifyMode.SSL_VERIFY_PEER |
          VerifyMode.SSL_VERIFY_FAIL_IF_NO_PEER_CERT,
          RemoteCertificateValidation);
 
      _ctx.UsePrivateKey(AsymmetricKeyToOpenSslFormat(_private_key));
      _ctx.UseCertificate(_osch.LocalCertificate);
      _ctx.CheckPrivateKey();

      _ctx.Options = SslOptions.SSL_OP_SINGLE_DH_USE;
      var rng = new RNGCryptoServiceProvider();
      byte[] sid = new byte[4];
      rng.GetBytes(sid);
      _ctx.SetSessionIdContext(sid);
      _ctx.SetCookieGenerateCallback(GenerateCookie);
      _ctx.SetCookieVerifyCallback(VerifyCookie);
      _ctx.Options = SslOptions.SSL_OP_COOKIE_EXCHANGE;
      UpdateCookie();
    }

    protected void UpdateCookie()
    {
      byte[] cookie = new byte[20];
      var rng = new RNGCryptoServiceProvider();
      rng.GetBytes(cookie);
      _cookie = MemBlock.Reference(cookie);
    }

    protected byte[] CalculateCookie(int uid)
    {
      byte[] cookie_base = _cookie;
      byte[] cookie_full = new byte[cookie_base.Length + 4];
      cookie_base.CopyTo(cookie_full, 0);
      NumberSerializer.WriteInt(uid, cookie_full, cookie_base.Length);
      SHA1 sha = new SHA1CryptoServiceProvider();
      return sha.ComputeHash(cookie_full);
    }

    protected int GenerateCookie(Ssl ssl, out byte[] cookie)
    {
      cookie = CalculateCookie(ssl.Handle.GetHashCode());
      return 1;
    }

    protected int VerifyCookie(Ssl ssl, byte[] cookie)
    {
      MemBlock from_cookie = MemBlock.Reference(cookie);
      byte[] lcookie = CalculateCookie(ssl.Handle.GetHashCode());
      MemBlock local_cookie = MemBlock.Reference(lcookie);
      if(local_cookie.Equals(from_cookie)) {
        return 1;
      } else {
        return -1;
      }
    }
 
    // Protected non-polymorphic methods

    // Public non-polymorphic methods

    static public CryptoKey AsymmetricKeyToOpenSslFormat(RSACryptoServiceProvider rsa)
    {
      // PEM, since OpenSSL doesn't easily allow unpassworded DER, for whatever reason
      string pkcs = RSAPrivateKeyToDER.RSAKeyToPEM(rsa.ExportParameters(true));
      CryptoKey key = CryptoKey.FromPrivateKey(pkcs, null);
      return key;
    }

    // Protected methods

    /// <summary></summary>
    protected bool NoSuchSA(ISender return_path, int remote_id, bool start,
        out DtlsAssociation sa)
    {
      sa = new DtlsAssociation(return_path, _ch, PType, new Ssl(_ctx), start);
      sa.StateChangeEvent += SAStateChange;
      sa.RemoteID = remote_id;
      return true;
    }

    /// <summary>Override to review certificate validation decisions.</summary>
    protected bool RemoteCertificateValidation(object sender,
        X509Certificate cert, X509Chain chain, int depth, VerifyResult result)
    {
      try {
        _ch.Verify(OpenSslCertificateHandler.OpenSslX509ToMonoX509(cert),
            default(ISender));
      } catch {
        return false;
      }
      return result == VerifyResult.X509_V_OK ||
        result == VerifyResult.X509_V_ERR_DEPTH_ZERO_SELF_SIGNED_CERT;
    }

    override protected void RemoveSA(SecurityAssociation sa)
    {
      DtlsAssociation dsa = sa as DtlsAssociation;
      if(dsa == null) {
        throw new Exception("SecurityAssociation is not a DtlsAssociation: " + sa);
      }

      _rwl.AcquireWriterLock(Timeout.Infinite);
      _sender_to_sa.Remove(dsa.Sender);
      _rwl.ReleaseWriterLock();
      _it.Remove(dsa.LocalID);
    }

    // Public overriden methods

    override public SecurityAssociation CreateSecurityAssociation(ISender sender, bool start)
    {
      DtlsAssociation sa = null;
      if(_sender_to_sa.TryGetValue(sender, out sa)) {
        return sa;
      }

      if(NoSuchSA(sender, 0, start, out sa)) {
        DtlsAssociation tmpsa = sa;
        bool close = false;
        lock(_sender_to_sa) {
          if(_sender_to_sa.TryGetValue(sender, out sa)) {
            close = true;
          } else {
            _sender_to_sa.Add(sender, tmpsa);
          }
        }

        if(close) {
          tmpsa.Close("Duplicate");
        } else {
          sa = tmpsa;
          _it.Add(sa);
          sa.Subscribe(this, null);
          sa.Handshake();
        }
      }

      return sa;
    }

    override public void HandleData(MemBlock data, ISender return_path, object state)
    {
      if(return_path is SecurityAssociation) {
        Handle(data, return_path);
        return;
      }

      MemBlock tmp = data.Slice(0, _ptype_mb.Length);
      if(tmp.Equals(_ptype_mb)) {
        data = data.Slice(_ptype_mb.Length);
      }

      MemBlock payload;
      int localid, remoteid;
      IIdentifierPair cp;
      DtlsAssociation sa;
      try {
        _it.Parse(data, out payload, out localid, out remoteid);
      } catch(Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, e.Message);
        return;
      }

      if(_it.TryGet(localid, remoteid, out cp)) {
        sa = cp as DtlsAssociation;
      } else {
        NoSuchSA(return_path, remoteid, false, out sa);
        _it.Add(sa);
        sa.RemoteID = remoteid;
        sa.Subscribe(this, null);
      }
      sa.HandleData(payload, return_path, null);
    }
  }

#if BRUNET_NUNIT
  public class DtlsOverlordClientServer {
    protected static RSACryptoServiceProvider _rsa;
    public RSACryptoServiceProvider Rsa {
      get {
        byte[] blob = _rsa.ExportCspBlob(true);
        RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        rsa.ImportCspBlob(blob);
        return rsa;
      }
    }

    public readonly static mx509.X509Certificate X509;
    static DtlsOverlordClientServer()
    {
      _rsa = new RSACryptoServiceProvider();
      byte[] blob = _rsa.ExportCspBlob(false);
      RSACryptoServiceProvider rsa_pub = new RSACryptoServiceProvider();
      rsa_pub.ImportCspBlob(blob);
      CertificateMaker cm = new CertificateMaker("United States", "UFL",
          "ACIS", "David Wolinsky", "davidiw@ufl.edu", rsa_pub,
          "brunet:node:abcdefghijklmnopqrs");
      Certificate cert = cm.Sign(cm, _rsa);
      X509 = cert.X509;
    }

    public static readonly PType PType = new PType("x");

    public readonly DtlsOverlord Client, Server;
    public readonly MockSender ToClient, ToServer;
    public readonly MockDataHandler ClientIn, ServerIn;

    public DtlsOverlordClientServer(bool threaded, bool good_client,
        bool good_server, double noisy)
    {
      var server_ch = new OpenSslCertificateHandler();
      if(good_server) {
        server_ch.AddSignedCertificate(X509);
      }
      server_ch.AddCACertificate(X509);
      Server = new DtlsOverlord(Rsa, server_ch, PType);
      ServerIn = new MockDataHandler();
      Server.Subscribe(ServerIn, null);

      var client_ch = new OpenSslCertificateHandler();
      if(good_client) {
        client_ch.AddCACertificate(X509);
      }
      client_ch.AddSignedCertificate(X509);
      Client = new DtlsOverlord(Rsa, client_ch, PType);
      ClientIn = new MockDataHandler();
      Client.Subscribe(ClientIn, null);

      if(threaded) {
        ToServer = new ThreadedMockSender(null, null, Server, 1, noisy);
        ToClient = new ThreadedMockSender(ToServer, null, Client, 1, noisy);
      } else {
        ToServer = new MockSender(null, null, Server, 1, noisy);
        ToClient = new MockSender(ToServer, null, Client, 1, noisy);
      }
      ToServer.ReturnPath = ToClient;
    }
  }

  [TestFixture]
  public class DtlsAssociationTest {
    public void NextReceived(MockDataHandler mdh, MemBlock current)
    {
      DateTime start = DateTime.UtcNow.AddSeconds(5);
      while(mdh.LastReceived == current && DateTime.UtcNow < start) {
        Thread.Sleep(50);
      }
    }

    [Test]
    public void SimpleTest()
    {
      var docs = new DtlsOverlordClientServer(true, true, true, 0);
      var to_server = docs.Client.CreateSecurityAssociation(docs.ToServer) as DtlsAssociation;
      Thread.Sleep(1000);

      Assert.AreEqual(to_server.SslState, SslState.OK, "Client.State == SslState.OK");

      byte[] msg = System.Text.Encoding.UTF8.GetBytes("HelloWorld!");

      MemBlock lastin = docs.ServerIn.LastReceived;
      MemBlock mmsg = MemBlock.Reference(msg);
      to_server.Send(mmsg);
      NextReceived(docs.ServerIn, lastin);
      Assert.AreEqual(docs.ServerIn.LastReceived, mmsg, "Client -> Server");
    }

    [Test]
    public void TestClientRenegotiate()
    {
      var docs = new DtlsOverlordClientServer(false, true, true, 0);
      var to_server = docs.Client.CreateSecurityAssociation(docs.ToServer) as DtlsAssociation;
      Assert.AreEqual(to_server.State, SecurityAssociation.States.Active, "Client.State == SslState.OK");

      byte[] msg = System.Text.Encoding.UTF8.GetBytes("HelloWorld!");
      MemBlock mmsg = MemBlock.Reference(msg);
      to_server.Send(mmsg);
      Assert.AreEqual(docs.ServerIn.LastReceived, mmsg, "Client -> Server");

      to_server.Renegotiate();
      to_server.Send(mmsg);
      Assert.AreEqual(to_server.State, SecurityAssociation.States.Active, "Client.State == SslState.OK 2");

      msg = System.Text.Encoding.UTF8.GetBytes("HelloWorld!!!");
      mmsg = MemBlock.Reference(msg);
      to_server.Send(mmsg);
      Assert.AreEqual(docs.ServerIn.LastReceived, mmsg, "Client -> Server 2");
    }

    [Test]
    public void TestLongTest()
    {
      var docs = new DtlsOverlordClientServer(false, true, true, 0);
      var to_server = docs.Client.CreateSecurityAssociation(docs.ToServer) as DtlsAssociation;
      Assert.AreEqual(to_server.State, SecurityAssociation.States.Active, "Client.State == SslState.OK");

      byte[] msg = new byte[1024];
      System.Random rand = new System.Random();
      rand.NextBytes(msg);
      MemBlock mmsg0 = MemBlock.Reference(msg);

      msg = new byte[1024];
      rand.NextBytes(msg);
      MemBlock mmsg1 = MemBlock.Reference(msg);

      Assert.AreEqual(to_server.SslState, SslState.OK, "Client.State == SslState.OK");

      for(int i = 0; i < Int16.MaxValue; i++) {
        if(i % 2 == 0) {
          to_server.Send(mmsg0);
          Assert.AreEqual(docs.ServerIn.LastReceived, mmsg0, "Client -> Server");
        } else {
          to_server.Send(mmsg1);
          Assert.AreEqual(docs.ServerIn.LastReceived, mmsg1, "Client -> Server");
        }
      }
    }

    [Test]
    public void TestBadClient()
    {
      var docs = new DtlsOverlordClientServer(true, false, true, 0);
      var to_server = docs.Client.CreateSecurityAssociation(docs.ToServer) as DtlsAssociation;

      DateTime end = DateTime.UtcNow.AddMinutes(4);
      while(end > DateTime.UtcNow && to_server.State != SecurityAssociation.States.Closed) {
        Thread.Sleep(1000);
      }

      Assert.AreEqual(to_server.State, SecurityAssociation.States.Closed, "Client closed");
      Assert.AreNotEqual(to_server.SslState, SslState.OK, "Client.State != SslState.OK");
    }

//    This test needs for us to create a second CA cert incompatible with the first...
//    [Test]
    public void TestBadServer()
    {
      var docs = new DtlsOverlordClientServer(true, true, false, 0);
      var to_server = docs.Client.CreateSecurityAssociation(docs.ToServer) as DtlsAssociation;

      DateTime end = DateTime.UtcNow.AddMinutes(4);
      while(end > DateTime.UtcNow && to_server.State != SecurityAssociation.States.Closed) {
        Thread.Sleep(1000);
      }

      Assert.AreEqual(to_server.State, SecurityAssociation.States.Closed, "Client closed");
      Assert.AreNotEqual(to_server.SslState, SslState.OK, "Client.State != SslState.OK");
    }

    [Test]
    public void SendBadPacket()
    {
      var docs = new DtlsOverlordClientServer(false, true, true, 0);
      var to_server = docs.Client.CreateSecurityAssociation(docs.ToServer) as DtlsAssociation;
      Assert.AreEqual(to_server.SslState, SslState.OK, "Client.State == SslState.OK");

      byte[] msg = System.Text.Encoding.UTF8.GetBytes("HelloWorld!");
      MemBlock mmsg = MemBlock.Reference(msg);
      to_server.Send(mmsg);
      Assert.AreEqual(docs.ServerIn.LastReceived, mmsg, "Client -> Server");
      to_server.Sender.Send(new CopyList(to_server.PType, to_server.Header, mmsg));
      Assert.AreEqual(docs.ServerIn.LastReceived, mmsg, "Client -> Server 2");
    }

//    This test frequently causes Assertion errors in openssl... they shouldn't exist, but oh well... it works sometimes...
//    [Test]
    public void TestLossyNetwork()
    {
      var docs = new DtlsOverlordClientServer(false, true, true, .5);
      var to_server = docs.Client.CreateSecurityAssociation(docs.ToServer) as DtlsAssociation;

      DateTime end = DateTime.UtcNow.AddSeconds(120);
      while(end > DateTime.UtcNow && !(to_server.SslState == SslState.OK)) {
        Thread.Sleep(1000);
      }

      Assert.AreEqual(to_server.State, SecurityAssociation.States.Active, "Client.State == SslState.OK");
    }
  }
#endif
}
