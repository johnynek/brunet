using Brunet.Messaging;
using Brunet.Security;
using Brunet.Util;
using mx509 = Mono.Security.X509;
using OpenSSL;
using OpenSSL.Core;
using OpenSSL.Crypto;
using OpenSSL.X509;
using System;
using System.Threading;

namespace Brunet.Security.Dtls {
  /// <summary>A Dtls filter for use with generic transports.</summary>
  public class DtlsAssociation : SecurityAssociation, IIdentifierPair {
    override public mx509.X509Certificate LocalCertificate {
      get {
        return OpenSslCertificateHandler.OpenSslX509ToMonoX509(_ssl.LocalCertificate);
      }
      set {
        throw new NotImplementedException("Certificate set internally");
      }
    }

    public int LocalID {
      get {
        return _ip.LocalID;
      }
      set {
        _ip.LocalID = value;
      }
    }

    override public mx509.X509Certificate RemoteCertificate {
      get {
        return OpenSslCertificateHandler.OpenSslX509ToMonoX509(_ssl.RemoteCertificate);
      }
      set {
        throw new NotImplementedException("Certificate set internally");
      }
    }

    public int RemoteID {
      get {
        return _ip.RemoteID;
      }
      set {
        _ip.RemoteID = value;
      }
    }

    public MemBlock Header { get { return _ip.Header; } }
    /// <summary></summary>
    public readonly PType PType;
    /// <summary>The current State of the DTLS state machine.</summary>
    public SslState SslState { get { return _ssl.State; } }

    protected byte[] _buffer;
    protected object _buffer_sync;
    protected bool _client;
    protected readonly IdentifierPair _ip;
    protected FuzzyEvent _fe;
    protected int _fe_lock;
    protected readonly BIO _read;
    protected readonly BIO _write;
    protected readonly Ssl _ssl;

    /// <summary>Create a DtlsFilter.</summary>
    /// <param name="key">A CryptoKey initialized by the OpenSSL.NET library.</param>
    /// <param name="cert">The path to the certificate to use.</param>
    /// <param name="ca_cert">The path to the ca certificate to use.</param>
    /// <param name="client">Use client initialization parameters.</param>
    public DtlsAssociation(ISender sender, CertificateHandler ch, PType ptype,
        Ssl ssl, bool client) : base(sender, ch)
    {
      _ip = new IdentifierPair();
      PType = ptype;
      _ssl = ssl;
      _client = client;
      _ssl.SetReadAhead(1);
      // Buggy SSL versions have issue with compression and dtls
      _ssl.SetOptions((int) SslOptions.SSL_OP_NO_COMPRESSION);
      if(client) {
        _ssl.SetConnectState();
      } else {
        _ssl.SetAcceptState();
      }

      // The ssl object will take control
      _read = BIO.MemoryBuffer(false);
      _read.NonBlocking = true;
      _write = BIO.MemoryBuffer(false);
      _write.NonBlocking = true;

      _ssl.SetBIO(_read, _write);
      _ssl.DoHandshake();

      _buffer = new byte[Int16.MaxValue];
      _buffer_sync = new object();
      _fe_lock = 0;
    }

    ~DtlsAssociation() {
      _ssl.Dispose();
    }

    override protected bool HandleIncoming(MemBlock data, out MemBlock app_data)
    {
      app_data = null;
      int count = 0;

      lock(_buffer_sync) {
        if(data != null) {
          data.CopyTo(_buffer, 0);
          _read.Write(_buffer, data.Length);
        }

        count = _ssl.Read(_buffer, _buffer.Length);
        if(count > 0) {
          app_data = MemBlock.Copy(_buffer, 0, count);
        }
      }

      if(app_data != null) {
        // If the read was successful, Dtls has received an incoming data
        // message and decrypted it
        return true;
      } else {
        SslError error = _ssl.GetError(count);
        if(error == SslError.SSL_ERROR_WANT_READ) {
          if(SslState == SslState.OK) {
            UpdateState(States.Active);
            // In the SslCtx verify, there's no way to get the underlying Sender
            _ch.Verify(RemoteCertificate, Sender);
          }
          HandleWouldBlock();
        } else if(error == SslError.SSL_ERROR_SSL) {
          var ose = new OpenSslException();
          Close("Received unrecoverable error: " + ose.ToString());
          throw ose;
        } else if(error == SslError.SSL_ERROR_ZERO_RETURN) {
          Close("Received clean close notification");
        } else {
          ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions,
              "Receive other: " + error);
        }
      }
      return false;
    }

    override protected bool HandleOutgoing(ICopyable app_data, out ICopyable data)
    {
      MemBlock buffer = null;
      data = null;
      int written = 1;
      lock(_buffer_sync) {
        if(app_data != null) {
          int count = app_data.CopyTo(_buffer, 0);
          written = _ssl.Write(_buffer, count);
        }

        if(written > 0) {
          int count = _write.Read(_buffer, _buffer.Length);
          if(count <= 0) {
            // This really shouldn't ever happen
            ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, this + " error");
            data = null;
            return false;
          }

          buffer = MemBlock.Copy(_buffer, 0, count);
        }
      }

      if(written > 0) {
        // Timer becomes -1 when there are no more control messages
        long to = _ssl.GetTimeout();
        if(to >= 0) {
          HandleWouldBlock();
        }

        if(buffer != null) {
          data = new CopyList(PType, Header, buffer);
          return true;
        }
      }

      // If the write failed, then Dtls is either waiting for a control message
      // or has a control message to send
      var error = _ssl.GetError(written);
      if(error == SslError.SSL_ERROR_WANT_READ) {
        HandleWouldBlock();
      } else if(error == SslError.SSL_ERROR_SSL) {
        var ose = new OpenSslException();
        Close("Received unrecoverable error: " + ose.ToString());
        throw ose;
      } else {
        ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, "Send other");
      }
      data = null;
      return false;
    }

    /// <summary>Blocking may occur for two reasons, no data available or the
    /// handshake isn't complete.  If the handshake isn't complete, the timer
    /// will be set to >= 0, at 0, we need to call handshake to continue, if
    /// it is greater than 0, we need to schedule a timer to call handshake
    /// later.</summary>
    protected void HandleWouldBlock()
    {
      if(Closed) {
        return;
      }
      long to = _ssl.GetTimeout();
      if(to == 0) {
        _ssl.TimerExpired();
      }
      if(_read.BytesPending > 0) {
        HandleData(null, Sender, null);
      }
      if(_write.BytesPending > 0) {
        Send(null);
      }

#if !BRUNET_SIMULATOR
      if(to > 0 && Interlocked.Exchange(ref _fe_lock, 1) == 0) {
        _fe = FuzzyTimer.Instance.DoAfter(HandleWouldBlock, (int) to, 250);
      }
#endif
    }

    /// <summary>Timercall back for internal ssl timeout.</summary>
    private void HandleWouldBlock(DateTime now)
    {
      Interlocked.Exchange(ref _fe_lock, 0);
      try {
        HandleWouldBlock();
      } catch (Exception e) {
        Close("Unhandled exception: " + e.ToString());
        ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions,
            this + "\n" + e.ToString());
      }
    }

    /// <summary>Override to review certificate validation decisions.</summary>
    protected bool RemoteCertificateValidation(object sender,
        X509Certificate cert, X509Chain chain, int depth, VerifyResult result)
    {
      return result == VerifyResult.X509_V_OK;
    }

    /// <summary>Start the session earlier than waiting for a packet to be sent.
    /// Not doing this may increase the amount of lost packets.</summary>
    public void Handshake()
    {
      HandleWouldBlock();
    }

    /// <summary>The session has gone on long enough that it is time for the
    /// peers to start using a new set of symmetric keys.</summary>
    public void Renegotiate()
    {
      if(Closed) {
        throw new Exception("Closed!");
      }

      UpdateState(States.Updating);
      _ssl.Renegotiate();
      _ssl.DoHandshake();
      if(!_client) {
        // Tends to cause a really nasty 
        _ssl.State = SslState.ACCEPT;
        _ssl.DoHandshake();
      }
    }

    override public string ToString()
    {
      if(_client) {
        return "Client: " + _ssl.StateStringLong() + " " + base.ToString();
      }
      return "Server: " + _ssl.StateStringLong() + " " + base.ToString();
    }
  }
}
