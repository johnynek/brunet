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
using Brunet.Transport;
using Brunet.Util;
using Mono.Security.X509;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

#if BRUNET_NUNIT
using Brunet.Messaging.Mock;
using NUnit.Framework;
using System.Threading;
#endif

namespace Brunet.Security.PeerSec {
  /// <summary>This is the brains of the operation.  User code can ask for a Secure
  /// Sender for a given sender, this will return one and begin the process of
  /// securing the sender.  Sending over a sender is only secure if it is done
  /// throug the secure sender.  On the other side, the user should ensure that
  /// the packet was sent via a secure sender at some point in the stack. </summary>
  public class PeerSecOverlord: SecurityOverlord, IReplyHandler {
    protected Dictionary<int, Dictionary<ISender, PeerSecAssociation>> _spi;
#if BRUNET_NUNIT
    // This allows us to review the SPI in testing mode
    public Dictionary<int, Dictionary<ISender, PeerSecAssociation>> SPI {
      get {
        return _spi;
      }
    }
#endif

    protected List<SecurityAssociation> _security_associations {
      get {
        List<SecurityAssociation> sas = new List<SecurityAssociation>();
        lock(_sync) {
          foreach(Dictionary<ISender, PeerSecAssociation> sender_to_sa in _spi.Values) {
            foreach(SecurityAssociation sa in sender_to_sa.Values) {
              sas.Add(sa);
            }
          }
        }
        return sas;
      }
    }

    override protected IEnumerable<SecurityAssociation> _sas {
      get {
        return _security_associations;
      }
    }

    protected readonly object _private_key_lock;
    protected byte[] _cookie;
    protected readonly Random _rand;
    protected readonly ReqrepManager _rrman;

    ///<summary>Security implementations version number.</summary>
    public static readonly int Version = 0;
    ///<summary>The length used for the cookies.</summary>
    public static readonly int CookieLength = 20;
    ///<summary>A quickly referenceable null (0) cookie.</summary>
    public static readonly MemBlock EmptyCookie;
    ///<summary>Since we may receive packets from an external MultiSource, all
    ///security packets are prepended with this ptype.</summary>
    public static readonly PType Security = new PType(29);
    ///<summary>A data packet to be handled by the SecurityAssociations.</summary>
    public static readonly PType SecureData = new PType(30);
    ///<summary>A control packet handled by the SecurityOverlord.</summary>
    public static readonly PType SecureControl = new PType(31);

    protected DateTime _last_heartbeat;

    ///<summary>Total count of all Security Associations.</summary>
    override public int SACount { get { return _security_associations.Count; } }

    static PeerSecOverlord() {
      byte[] cookie = new byte[CookieLength];
      for(int i = 0; i < CookieLength; i++) {
        cookie[i] = 0;
      }
      EmptyCookie = MemBlock.Reference(cookie);
    }

    public PeerSecOverlord(RSACryptoServiceProvider rsa, CertificateHandler ch,
        ReqrepManager rrman) : base(rsa, ch)
    {
      _private_key_lock = new object();
      _spi = new Dictionary<int, Dictionary<ISender, PeerSecAssociation>>();
      _cookie = new byte[CookieLength];
      _rand = new Random();
      _rand.NextBytes(_cookie);
      _rrman = rrman;
      _last_heartbeat = DateTime.UtcNow;
      _rrman.Subscribe(this, null);
    }

    /// <summary>Removes the specified SA from our database.</summary>
    override protected void RemoveSA(SecurityAssociation sa)
    {
      PeerSecAssociation psa = sa as PeerSecAssociation;
      if(psa == null) {
        throw new Exception("Invalid PeerSecAssociation: " + sa);
      }
      lock(_sync) {
        if(_spi.ContainsKey(psa.SPI)) {
          _spi[psa.SPI].Remove(psa.Sender);
        }
      }
    }

    /// <summary>When an SA wants to be updated, we instigate a new Security
    /// exchange.</summary>
    protected void SARequestUpdate(object o, EventArgs ea) {
      PeerSecAssociation sa = o as PeerSecAssociation;
      if(sa != null) {
        StartSA(sa);
      }
    }

    /// <summary>This (idempotently) returns a new SecurityAssociation for the
    /// specified sender using the default SPI and starts it if requested to.</summary>
    override public SecurityAssociation CreateSecurityAssociation(ISender Sender) {
      return CreateSecurityAssociation(Sender, SecurityPolicy.DefaultSPI);
    }

    /// <summary>This (idempotently) returns a new SecurityAssociation for the
    /// specified sender using the specified SPI and starts it if requested to.</summary>
    virtual public PeerSecAssociation CreateSecurityAssociation(ISender Sender, int SPI) {
      return CreateSecurityAssociation(Sender, SPI, true);
    }

    /// <summary>This (idempotently) returns a new SecurityAssociation for the
    /// specified sender using the specified SA.</summary>
    protected PeerSecAssociation CreateSecurityAssociation(ISender Sender,
        int SPI, bool start)
    {
      if(!SecurityPolicy.Supports(SPI)) {
        throw new Exception("Unsupported SPI");
      }

      PeerSecAssociation sa = null;
      lock(_sync) {
        Dictionary<ISender, PeerSecAssociation> sender_to_sa = null;
        if(_spi.ContainsKey(SPI)) {
          sender_to_sa = _spi[SPI];
        } else {
          sender_to_sa = new Dictionary<ISender, PeerSecAssociation>();
          _spi[SPI] = sender_to_sa;
        }

        if(sender_to_sa.ContainsKey(Sender)) {
          sa = sender_to_sa[Sender];
        } else {
          sa = new PeerSecAssociation(Sender, _ch, SPI);
          sa.Subscribe(this, null);
          sa.StateChangeEvent += SAStateChange;
          sa.RequestUpdate += SARequestUpdate;
          sender_to_sa[Sender] = sa;
        }
      }

      if(start && sa.State != SecurityAssociation.States.Active) {
        StartSA(sa);
      }
      return sa;
    }

    /// <summary>This begins the SecurityAssociation exchange protocol over the
    /// specified SecurityAssociation.</summary>
    protected void StartSA(PeerSecAssociation sa) {
      SecurityControlMessage scm_reply = new SecurityControlMessage();
      scm_reply.Version = Version;
      scm_reply.SPI = sa.SPI;
      scm_reply.Type = SecurityControlMessage.MessageType.Cookie;
      scm_reply.LocalCookie = CalculateCookie(sa.Sender);

      ICopyable to_send = new CopyList(Security, SecureControl, scm_reply.Packet);

      _rrman.SendRequest(sa.Sender, ReqrepManager.ReqrepType.Request,
          to_send, this, sa);
    }

    /// <summary>After a restart of the Security system, one guy may think
    /// we still have an association and there will be no way for him to know
    /// that our side is broken, unless we notify him as such.  We notify him
    /// by sending this packet.  How he deals with that is up to him.</summary>
    protected void NoSuchSA(int spi, ISender remote_sender) {
      SecurityControlMessage scm_reply = new SecurityControlMessage();
      scm_reply.Version = Version;
      scm_reply.SPI = spi;
      scm_reply.Type = SecurityControlMessage.MessageType.NoSuchSA;
      ICopyable to_send = new CopyList(Security, SecureControl, scm_reply.Packet);
      remote_sender.Send(to_send);
    }

    /// <summary>All messages for the SecurityOverlord come through this loop.
    /// It demuxes between Security, SecureData, and SecureControl packets, while
    /// the remaining packets are left to the default handler.</summary>
    override public void HandleData(MemBlock data, ISender return_path, object state) {
      MemBlock payload = null;
      PType t = null;
      try {
        t = PType.Parse(data, out payload);

        if(t.Equals(Security)) {
          HandleData(payload, return_path, null);
        } else if(t.Equals(SecureData)) {
          HandleData(payload, return_path);
        } else if(t.Equals(SecureControl)) {
          HandleControl(payload, return_path);
        } else if(t.Equals(PType.Protocol.ReqRep)) {
          _rrman.HandleData(payload, return_path, null);
        } else {
          Edge edge = return_path as Edge;
          if(edge != null && !(edge is Brunet.Security.Transport.SecureEdge)) {
            throw new Exception("Insecure edge attempting to communicate with the node!");
          }
          Subscriber sub = _sub;
          if(sub == null) {
            throw new Exception("No default handler... this won't do!");
          }
          _sub.Handle(data, return_path);
        }
      } catch(Exception e) {
        string ps = string.Empty;
        try {
          ps = payload.GetString(System.Text.Encoding.ASCII);
        } catch { }
        ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, String.Format(
              "Security Packet Handling Exception, Type: {0}, From: {1}\n\tData: {2}\n\tException:{3}\n\tStack Trace:{4}",
              t, return_path, ps, e, new System.Diagnostics.StackTrace(true)));
      }
    }

    /// <summary>This is SecureData that needs to get to an SA.</summary>
    protected void HandleData(MemBlock b, ISender return_path) {
      SecurityDataMessage sdm = new SecurityDataMessage(b);
      Dictionary<ISender, PeerSecAssociation> sender_to_sa = null;
      PeerSecAssociation sa = null;
      try {
        sender_to_sa = _spi[sdm.SPI];
        sa = sender_to_sa[return_path];
        sa.HandleData(b, return_path, null);
      } catch(Exception e) {
        if(sender_to_sa == null && !SecurityPolicy.Supports(sdm.SPI)) {
          ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, String.Format(
                "Unsupported SPI, from {0}, message: {1}", return_path, sdm));
        } else {
          if(sa == null) {
            ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, String.Format(
                  "No SA, from {0}, message: {1}", return_path, sdm));
          } else if(sa.Closed) {
            ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, String.Format(
                  "SA, {0}, has been closed, from {1}, message: {2}.", sa,
                  return_path, sdm));
          } else {
            ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, String.Format(
                  "SA, {0}, from {1}, message: {2}, causes unhandled exception : {3}",
                  sa, return_path, sdm, e));
          }
          NoSuchSA(sdm.SPI, return_path);
        }
      }
    }

    /// <summary>This better be a SecureControl message!</summary>
    public bool HandleReply(ReqrepManager man, ReqrepManager.ReqrepType rt,
         int mid,
         PType prot,
         MemBlock payload, ISender returnpath,
         ReqrepManager.Statistics statistics,
         object state)
    {
      if(!prot.Equals(SecureControl)) {
        return true;
      }

      bool done = false;
      try {
        HandleControl(payload, returnpath);
      } catch(Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, e.ToString());
        done = true;
      }

      return done;
    }

    /// <summary>If the request really failed, we'll have to close the SA.</summary>
    public void HandleError(ReqrepManager man, int message_number,
         ReqrepManager.ReqrepError err, ISender returnpath, object state)
    {
      if(man.RequestActive(message_number)) {
        return;
      }

      PeerSecAssociation sa = state as PeerSecAssociation;
      ProtocolLog.WriteIf(ProtocolLog.Security, "Forcing reset due to timeout: " + sa);
      sa.Reset();
    }

    /// <summary>This is the control state machine.  There are three paths in
    /// the state machine, iniator, receiver, and bidirectional.  The
    /// bidirectional case occurs when two remote ISenders that are matched
    /// together initiate a handshake at the same time, otherwise the initiator
    /// /receiver pattern is followed.  The high level overview for the states
    /// are:
    /// 1a) Send a Cookie
    /// 1b) Receive a Cookie which responds with a CookieResponse
    /// 2a) Receive a CookieResponse that contains a list of CAs, if you have
    /// a Certificate that supports one of the CAs send it along with a DHE
    /// and a list of your supported CAs in a DHEWithCertificateAndCAs.
    /// 2b) Receive a DHEWithCertificateAndCAs, verify the certificate and attempt
    /// to find a matching Certificate for the list of CAs, if you find one,
    /// finish the DHE handshake and send the certificate via a DHEWithCertificate
    /// 3a) Receive a DHEWithCertificate, verify the certificate and DHE and
    /// send a Confirm that you are ready to Verify the stack and start the
    /// system.
    /// 3b) Receive a Confirm, verify the entire stack and send a Confirm
    /// 4a)Receive a Confirm, verify the entire stack and all set to go
    /// </summary>
    protected void HandleControl(MemBlock b, ISender return_path) {
      ISender low_level_sender = return_path;
      if(low_level_sender is ReqrepManager.ReplyState) {
        low_level_sender = ((ReqrepManager.ReplyState) low_level_sender).ReturnPath;
      }

      SecurityControlMessage scm = new SecurityControlMessage(b);
      MemBlock calc_cookie = CalculateCookie(low_level_sender);

      if(scm.Version != Version) {
        ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, String.Format(
              "Invalid version expected {0}, found {1}, from {2}, message: {3}",
              Version, scm.Version, low_level_sender, scm));
        return;
      } else if(!SecurityPolicy.Supports(scm.SPI)) {
        ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, String.Format(
              "Unsupported SPI, from {0}, message: {1}", low_level_sender, scm));
        return;
      } else if((scm.Type != SecurityControlMessage.MessageType.Cookie &&
            scm.Type != SecurityControlMessage.MessageType.NoSuchSA) &&
          !scm.RemoteCookie.Equals(calc_cookie))
      {
        ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, String.Format(
              "Invalid cookie, Expected {0}, Found {1}, From {2}, Message {3}",
              calc_cookie, scm.RemoteCookie, low_level_sender, scm));
        return;
      }

      SecurityControlMessage scm_reply = new SecurityControlMessage();
      scm_reply.Version = Version;
      scm_reply.SPI = scm.SPI;

      PeerSecAssociation sa = null;
      // This can be in a try statement since this is best effort anyway
      try {
        Dictionary<ISender, PeerSecAssociation> sender_to_sa = _spi[scm.SPI];
        sa = sender_to_sa[low_level_sender];
      } catch { }

      if(sa != null) {
        // Reset cases where no state has been set yet or state is attempting
        // to be re-established, if other message types arrive, they will
        // expect pre-set state and will fail.  The mechanisms instilled thus
        // far should prevent this, but this makes it explicit.  Now only
        // wayward messages will arrive to other states.
        if(scm.Type == SecurityControlMessage.MessageType.NoSuchSA ||
            scm.Type == SecurityControlMessage.MessageType.Cookie ||
            scm.Type == SecurityControlMessage.MessageType.CookieResponse ||
            scm.Type == SecurityControlMessage.MessageType.DHEWithCertificateAndCAs)
        {
          sa.TryReset();
        }

        if(sa.Closed) {
          ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, GetHashCode() + " SA closed, clearing SA state! " + sa);
          sa = null;
        } else if(sa.State == SecurityAssociation.States.Active) {
          ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, String.Format(
                "{0}, {1}: {2}", GetHashCode(), "SA Active, received message",
                scm.Type));
          return;
        }
      }

      try {
        switch(scm.Type) {
          case SecurityControlMessage.MessageType.NoSuchSA:
            HandleControlNoSuchSA(sa);
            break;
          case SecurityControlMessage.MessageType.Cookie:
            HandleControlCookie(sa, calc_cookie, scm, scm_reply, return_path, low_level_sender);
            break;
          case SecurityControlMessage.MessageType.CookieResponse:
            HandleControlCookieResponse(sa, scm, scm_reply, return_path, low_level_sender);
            break;
          case SecurityControlMessage.MessageType.DHEWithCertificateAndCAs:
            HandleControlDHEWithCertificateAndCAs(sa, scm, scm_reply, return_path, low_level_sender);
            break;
          case SecurityControlMessage.MessageType.DHEWithCertificate:
            HandleControlDHEWithCertificate(sa, scm, scm_reply, return_path, low_level_sender);
            break;
          case SecurityControlMessage.MessageType.Confirm:
            HandleControlConfirm(sa, scm, scm_reply, return_path, low_level_sender);
            break;
          default:
            throw new Exception("Invalid message!");
        }
      } catch(Exception e) {
        if(scm.Type == SecurityControlMessage.MessageType.DHEWithCertificateAndCAs ||
            scm.Type == SecurityControlMessage.MessageType.DHEWithCertificate ||
            scm.Type == SecurityControlMessage.MessageType.Confirm)
        {
          NoSuchSA(scm.SPI, return_path);
        }
        if(sa != null && sa.Closed) {
          ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, GetHashCode() + " SA closed! " + sa);
          return;
        }
        ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, String.Format(
              "Error in {0}, unhandled exception from {1}, message: {2}, exception: {3}",
              GetHashCode(), low_level_sender, scm, e));
      }
    }

    /// <summary>1a) Send a Cookie</summary>
    /// <param name="sa">A security association that we wish to perform the
    /// specified control operation on.</param>
    protected void HandleControlNoSuchSA(PeerSecAssociation sa)
    {
      if(sa == null) {
        ProtocolLog.WriteIf(ProtocolLog.Security, GetHashCode() + " NoSuchSA received, but we have no SA either!");
      } else {
        ProtocolLog.WriteIf(ProtocolLog.Security, GetHashCode() + " NoSuchSA received, handling...");
        StartSA(sa);
      }
    }

    /// <summary>1b) Receive a Cookie which responds with a CookieResponse</summary>
    /// <param name="sa">A security association that we wish to perform the
    /// specified control operation on.</param>
    /// <param name="calc_cookie">Cookie value for the association sender.</param>
    /// <param name="scm">The received SecurityControlMessage.</param>
    /// <param name="scm_reply">A prepared reply message (with headers and such.</param>
    /// <param name="return_path">Where to send the result.</param>
    /// <param name="low_level_sender">We expect the return_path to not be an edge or
    /// some other type of "low level" sender, so this contains the parsed out value.</param>
    protected void HandleControlCookie(PeerSecAssociation sa,
        MemBlock calc_cookie, SecurityControlMessage scm,
        SecurityControlMessage scm_reply, ISender return_path,
        ISender low_level_sender)
    {
      ProtocolLog.WriteIf(ProtocolLog.Security, GetHashCode() + " Received Cookie from: " + low_level_sender);
      scm_reply.Type = SecurityControlMessage.MessageType.CookieResponse;
      scm_reply.RemoteCookie = scm.LocalCookie;
      scm_reply.LocalCookie = calc_cookie;
      if(SecurityPolicy.GetPolicy(scm.SPI).PreExchangedKeys) {
        scm_reply.CAs = new List<MemBlock>(0);
      } else {
        scm_reply.CAs = _ch.SupportedCAs;
      }
      ICopyable to_send = new CopyList(SecureControl, scm_reply.Packet);
      return_path.Send(to_send);
      ProtocolLog.WriteIf(ProtocolLog.Security, GetHashCode() + " Successful Cookie from: " + low_level_sender);
    }

    /// <summary>2a) Receive a CookieResponse that contains a list of CAs, if you have
    /// a Certificate that supports one of the CAs send it along with a DHE
    /// and a list of your supported CAs in a DHEWithCertificateAndCAs.</summary>
    /// <param name="sa">A security association that we wish to perform the
    /// specified control operation on.</param>
    /// <param name="scm">The received SecurityControlMessage.</param>
    /// <param name="scm_reply">A prepared reply message (with headers and such.</param>
    /// <param name="return_path">Where to send the result.</param>
    /// <param name="low_level_sender">We expect the return_path to not be an edge or
    /// some other type of "low level" sender, so this contains the parsed out value.</param>
    protected void HandleControlCookieResponse(PeerSecAssociation sa,
        SecurityControlMessage scm, SecurityControlMessage scm_reply,
        ISender return_path, ISender low_level_sender)
    {
      ProtocolLog.WriteIf(ProtocolLog.Security, GetHashCode() + " Received CookieResponse from: " + low_level_sender);
      if(sa == null) {
        throw new Exception("No valid SA!");
      }
      // This seems like unnecessary code
      scm_reply.Type = SecurityControlMessage.MessageType.CookieResponse;
      X509Certificate lcert = null;
      if(SecurityPolicy.GetPolicy(scm.SPI).PreExchangedKeys) {
        lcert = _ch.DefaultCertificate;
      } else {
        lcert = _ch.FindCertificate(scm.CAs);
      }

      sa.RemoteCookie.Value = scm.LocalCookie;
      sa.LocalCertificate = lcert;
      scm_reply.Certificate = lcert.RawData;

      scm_reply.DHE = sa.LDHE;
      scm_reply.LocalCookie = scm.RemoteCookie;
      scm_reply.RemoteCookie = scm.LocalCookie;
      scm_reply.Type = SecurityControlMessage.MessageType.DHEWithCertificateAndCAs;
      if(SecurityPolicy.GetPolicy(scm.SPI).PreExchangedKeys) {
        scm_reply.CAs = new List<MemBlock>(0);
      } else {
        scm_reply.CAs = _ch.SupportedCAs;
      }
      HashAlgorithm sha1 = new SHA1CryptoServiceProvider();
      lock(_private_key_lock) {
        scm_reply.Sign(_private_key, sha1);
      }

      sa.DHEWithCertificateAndCAsOutHash.Value = sha1.ComputeHash((byte[]) scm_reply.Packet);
      ICopyable to_send = new CopyList(Security, SecureControl, scm_reply.Packet);
      _rrman.SendRequest(return_path, ReqrepManager.ReqrepType.Request,
          to_send, this, sa);
      ProtocolLog.WriteIf(ProtocolLog.Security, GetHashCode() + " Successful CookieResponse from: " + low_level_sender);
    }

    /// <summary>2b) Receive a DHEWithCertificateAndCAs, verify the certificate and attempt
    /// to find a matching Certificate for the list of CAs, if you find one,
    /// finish the DHE handshake and send the certificate via a DHEWithCertificate</summary>
    /// <param name="sa">A security association that we wish to perform the
    /// specified control operation on.</param>
    /// <param name="scm">The received SecurityControlMessage.</param>
    /// <param name="scm_reply">A prepared reply message (with headers and such.</param>
    /// <param name="return_path">Where to send the result.</param>
    /// <param name="low_level_sender">We expect the return_path to not be an edge or
    /// some other type of "low level" sender, so this contains the parsed out value.</param>
    protected void HandleControlDHEWithCertificateAndCAs(PeerSecAssociation sa,
        SecurityControlMessage scm, SecurityControlMessage scm_reply,
        ISender return_path, ISender low_level_sender)
    {
      ProtocolLog.WriteIf(ProtocolLog.Security, GetHashCode() + " Received DHEWithCertificateAndCAs from: " + low_level_sender);
      if(sa == null) {
        sa = CreateSecurityAssociation(low_level_sender, scm.SPI, false);
      }
      byte[] cert = new byte[scm.Certificate.Length];
      scm.Certificate.CopyTo(cert, 0);
      X509Certificate rcert = new X509Certificate(cert);
      _ch.Verify(rcert, low_level_sender);
      HashAlgorithm sha1 = new SHA1CryptoServiceProvider();
      scm.Verify((RSACryptoServiceProvider) rcert.RSA, sha1);

      X509Certificate lcert = null;
      if(SecurityPolicy.GetPolicy(scm.SPI).PreExchangedKeys) {
        lcert = _ch.DefaultCertificate;
      } else {
        lcert = _ch.FindCertificate(scm.CAs);
      }

      sa.LocalCertificate = lcert;
      sa.RemoteCertificate = rcert;
      sa.RDHE.Value = scm.DHE;
      sa.DHEWithCertificateAndCAsInHash.Value = MemBlock.Reference(sha1.ComputeHash((byte[]) scm.Packet));

      scm_reply.LocalCookie = scm.RemoteCookie;
      scm_reply.RemoteCookie = scm.LocalCookie;
      scm_reply.DHE = sa.LDHE;
      scm_reply.Certificate = MemBlock.Reference(lcert.RawData);
      scm_reply.Type = SecurityControlMessage.MessageType.DHEWithCertificate;
      lock(_private_key_lock) {
        scm_reply.Sign(_private_key, sha1);
      }
      sa.DHEWithCertificateHash.Value = MemBlock.Reference(sha1.ComputeHash((byte[]) scm_reply.Packet));

      ICopyable to_send = new CopyList(SecureControl, scm_reply.Packet);
      return_path.Send(to_send);
      ProtocolLog.WriteIf(ProtocolLog.Security, GetHashCode() + " Successful DHEWithCertificateAndCAs from: " + low_level_sender);
    }

    /// <summary>3a) Receive a DHEWithCertificate, verify the certificate and DHE and
    /// send a Confirm that you are ready to Verify the stack and start the
    /// system.</summary>
    /// <param name="sa">A security association that we wish to perform the
    /// specified control operation on.</param>
    /// <param name="scm">The received SecurityControlMessage.</param>
    /// <param name="scm_reply">A prepared reply message (with headers and such.</param>
    /// <param name="return_path">Where to send the result.</param>
    /// <param name="low_level_sender">We expect the return_path to not be an edge or
    /// some other type of "low level" sender, so this contains the parsed out value.</param>
    protected void HandleControlDHEWithCertificate(PeerSecAssociation sa,
        SecurityControlMessage scm, SecurityControlMessage scm_reply,
        ISender return_path, ISender low_level_sender)
    {
      ProtocolLog.WriteIf(ProtocolLog.Security, GetHashCode() + " Received DHEWithCertificate from: " + low_level_sender);
      if(sa == null) {
        throw new Exception("No valid SA!");
      }
      byte[] cert = new byte[scm.Certificate.Length];
      scm.Certificate.CopyTo(cert, 0);
      X509Certificate rcert = new X509Certificate(cert);
      HashAlgorithm sha1 = new SHA1CryptoServiceProvider();
      scm.Verify((RSACryptoServiceProvider) rcert.RSA, sha1);
      _ch.Verify(rcert, low_level_sender);

      sa.RemoteCertificate = rcert;
      sa.RDHE.Value = scm.DHE;

      scm_reply.LocalCookie = scm.RemoteCookie;
      scm_reply.RemoteCookie = scm.LocalCookie;
      scm_reply.Hash = MemBlock.Reference(sha1.ComputeHash((byte[]) scm.Packet));
      scm_reply.Type = SecurityControlMessage.MessageType.Confirm;
      lock(_private_key_lock) {
        scm_reply.Sign(_private_key, sha1);
      }

      ICopyable to_send = new CopyList(Security, SecureControl, scm_reply.Packet);
      _rrman.SendRequest(return_path, ReqrepManager.ReqrepType.Request,
          to_send, this, sa);
      ProtocolLog.WriteIf(ProtocolLog.Security, GetHashCode() + " Successful DHEWithCertificate from: " + low_level_sender);
    }

    /// <summary>3b) Receive a Confirm, verify the entire stack and send a Confirm
    /// 4a)Receive a Confirm, verify the entire stack and all set to go</summary>
    /// <param name="sa">A security association that we wish to perform the
    /// specified control operation on.</param>
    /// <param name="scm">The received SecurityControlMessage.</param>
    /// <param name="scm_reply">A prepared reply message (with headers and such.</param>
    /// <param name="return_path">Where to send the result.</param>
    /// <param name="low_level_sender">We expect the return_path to not be an edge or
    /// some other type of "low level" sender, so this contains the parsed out value.</param>
    protected void HandleControlConfirm(PeerSecAssociation sa,
        SecurityControlMessage scm, SecurityControlMessage scm_reply,
        ISender return_path, ISender low_level_sender)
    {
      ProtocolLog.WriteIf(ProtocolLog.Security, GetHashCode() + " Received Confirm from: " + low_level_sender);
      if(sa == null) {
        throw new Exception("No valid SA!");
      }
      HashAlgorithm sha1 = new SHA1CryptoServiceProvider();
      scm.Verify((RSACryptoServiceProvider) sa.RemoteCertificate.RSA, sha1);

      if(return_path == low_level_sender) {
        sa.VerifyResponse(scm.Hash);
      } else {
        sa.VerifyRequest(scm.Hash);
        scm_reply.LocalCookie = scm.RemoteCookie;
        scm_reply.RemoteCookie = scm.LocalCookie;
        scm_reply.Hash = sa.DHEWithCertificateAndCAsInHash.Value;
        scm_reply.Type = SecurityControlMessage.MessageType.Confirm;
        lock(_private_key_lock) {
          scm_reply.Sign(_private_key, sha1);
        }
        ICopyable to_send = new CopyList(SecureControl, scm_reply.Packet);
        return_path.Send(to_send);
      }
      sa.Enable();

      ProtocolLog.WriteIf(ProtocolLog.Security, GetHashCode() + " Successful Confirm from: " + low_level_sender);
    }

    /// <summary>We take in an object, take its hash code, concatenate it
    /// to our cookie, then sha hash the resulting value, creating the remote
    /// cookie.</summary>
    public MemBlock CalculateCookie(object o) {
      int hash = o.GetHashCode();
      byte[] data = new byte[4 + _cookie.Length];
      _cookie.CopyTo(data, 0);
      NumberSerializer.WriteInt(hash, data, _cookie.Length);
      HashAlgorithm sha1 = new SHA1CryptoServiceProvider();
      byte[] cookie = sha1.ComputeHash(data);
      return MemBlock.Reference(cookie);
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class PeerSecOverlordTest {
    event EventHandler _timeout;

    protected void Timeout(object o) {
      if(_timeout != null) {
        _timeout(null, null);
      }
    }

    protected void AnnounceSA(SecurityAssociation sa,
        SecurityAssociation.States state)
    {
    //  PeerSecAssociation sa = o as PeerSecAssociation;
    }

    RSACryptoServiceProvider rsa;
    RSACryptoServiceProvider rsa_safe {
      get {
        byte[] blob = rsa.ExportCspBlob(true);
        RSACryptoServiceProvider rrsa = new RSACryptoServiceProvider();
        rrsa.ImportCspBlob(blob);
        return rrsa;
      }
    }

    X509Certificate x509;

    protected PeerSecOverlord CreateInvalidSO(string name, int level) {
      if(rsa == null) {
        rsa = new RSACryptoServiceProvider();
        byte[] blob = rsa.ExportCspBlob(false);
        RSACryptoServiceProvider rsa_pub = new RSACryptoServiceProvider();
        rsa_pub.ImportCspBlob(blob);
        CertificateMaker cm = new CertificateMaker("United States", "UFL", 
            "ACIS", "David Wolinsky", "davidiw@ufl.edu", rsa_pub,
            "brunet:node:abcdefghijklmnopqrs");
        Certificate cert = cm.Sign(cm, rsa);
        x509 = cert.X509;
      }

      CertificateHandler ch = new CertificateHandler();
      if(level == 2 || level == 0) {
        ch.AddCACertificate(x509);
      }
      if(level == 3 || level == 0) {
        ch.AddSignedCertificate(x509);
      }
      Random rand = new Random();
      ReqrepManager rrm = new ReqrepManager("so" + name + rand.Next());
      _timeout += rrm.TimeoutChecker;

      PeerSecOverlord so = new PeerSecOverlord(rsa_safe, ch, rrm);
      so.AnnounceSA += AnnounceSA;
      RoutingDataHandler rdh = new RoutingDataHandler();
      rrm.Subscribe(so, null);
      so.Subscribe(rdh, null);
      rdh.Subscribe(rrm, null);
      return so;
    }

    protected PeerSecOverlord CreateValidSO(string name) {
      return CreateInvalidSO(name, 0);
    }

    [Test]
    public void TestWithPreExchangedKeys() {
      Timer t = new Timer(Timeout, null, 0, 500);
      int spi = 123333;
      new SecurityPolicy(spi, "Rijndael", "SHA1", true);
      PeerSecOverlord so0 = CreateValidSO("valid0");
      PeerSecOverlord so1 = CreateValidSO("valid1");

      MockSender ms0 = new MockSender(null, null, so1, 0);
      MockSender ms1 = new MockSender(ms0, null, so0, 0);
      ms0.ReturnPath = ms1;

      SecurityAssociation sa0 = so0.CreateSecurityAssociation(ms0, spi);
      SecurityAssociation sa1 = so1.CreateSecurityAssociation(ms1, spi);
      Assert.AreEqual(sa0.State, SecurityAssociation.States.Active, "sa0 should be active!");
      Assert.AreEqual(sa1.State, SecurityAssociation.States.Active, "sa1 should be active!");
      Assert.AreEqual(so0.SACount, 1, "so0 should contain just one!");
      Assert.AreEqual(so1.SACount, 1, "so1 should contain just one!");

      t.Dispose();
    }

    [Test]
    public void TestRemoteRestart() {
      Timer t = new Timer(Timeout, null, 0, 500);
      int spi = 123333;
      new SecurityPolicy(spi, "Rijndael", "SHA1", true);
      PeerSecOverlord so0 = CreateValidSO("valid0");
      PeerSecOverlord so1 = CreateValidSO("valid1");

      MockSender ms0 = new MockSender(null, null, so1, 0);
      MockSender ms1 = new MockSender(ms0, null, so0, 0);
      ms0.ReturnPath = ms1;

      SecurityAssociation sa0 = so0.CreateSecurityAssociation(ms0, spi);
      SecurityAssociation sa1 = so1.CreateSecurityAssociation(ms1, spi);
      Assert.AreEqual(sa0.State, SecurityAssociation.States.Active, "sa0 should be active!");
      Assert.AreEqual(sa1.State, SecurityAssociation.States.Active, "sa1 should be active!");

      sa0.CheckState();
      sa1.CheckState();
      sa1.Send(MemBlock.Reference(new byte[] {0, 1, 2, 3}));

      Assert.AreEqual(so0.SACount, 1, "so0 should contain just one! 0");
      Assert.AreEqual(so1.SACount, 1, "so1 should contain just one! 0");

      sa0.CheckState();
      sa0.CheckState();
      sa1.CheckState();
      Assert.AreEqual(so0.SACount, 0, "so0 should contain just zero!");
      Assert.AreEqual(so1.SACount, 1, "so1 should contain just one! 1");

      sa1.Send(MemBlock.Reference(new byte[] {0, 1, 2, 3}));
      Assert.AreEqual(so0.SACount, 1, "so0 should contain just one! 2");
      Assert.AreEqual(so1.SACount, 1, "so1 should contain just one! 1");

      t.Dispose();
    }

    [Test]
    public void Test() {
      Timer t = new Timer(Timeout, null, 0, 500);
      PeerSecOverlord so0 = CreateValidSO("valid0");
      PeerSecOverlord so1 = CreateValidSO("valid1");

      //Test block one
      {
        MockSender ms0 = new MockSender(null, null, so1, 0);
        MockSender ms1 = new MockSender(ms0, null, so0, 0);
        ms0.ReturnPath = ms1;

        SecurityAssociation sa0 = so0.CreateSecurityAssociation(ms0);
        SecurityAssociation sa1 = so1.CreateSecurityAssociation(ms1);
        Assert.AreEqual(sa0.State, SecurityAssociation.States.Active, "sa0 should be active!");
        Assert.AreEqual(sa1.State, SecurityAssociation.States.Active, "sa1 should be active!");
        Assert.AreEqual(so0.SACount, 1, "so0 should contain just one!");
        Assert.AreEqual(so1.SACount, 1, "so1 should contain just one!");

        Random rand = new Random();
        byte[] b = new byte[128];
        rand.NextBytes(b);
        MemBlock mb = MemBlock.Reference(b);
        sa1.Send(mb);

        new SecurityPolicy(12345, "DES", "MD5");
        sa0 = so0.CreateSecurityAssociation(ms0, 12345);
        Assert.AreEqual(sa0.State, SecurityAssociation.States.Active, "sa0 should be active!");
        Assert.AreEqual(so0.SACount, 2, "so0 should contain just one!");
        Assert.AreEqual(so1.SACount, 2, "so1 should contain just one!");

        b = new byte[128];
        rand.NextBytes(b);
        mb = MemBlock.Reference(b);
        sa0.Send(mb);
      }

      // create ~250 valid SAs for one guy...
      for(int i = 2; i < 250; i++) {
        PeerSecOverlord so = CreateValidSO("valid" + i);
        MockSender msa = new MockSender(null, null, so, 0);
        MockSender msb = new MockSender(msa, null, so0, 0);
        msa.ReturnPath = msb;

        SecurityAssociation sab = so.CreateSecurityAssociation(msb);
        Assert.AreEqual(sab.State, SecurityAssociation.States.Active, "sab should be active! " + i);
        SecurityAssociation saa = so0.CreateSecurityAssociation(msa);
        Assert.AreEqual(saa.State, SecurityAssociation.States.Active, "saa should be active! " + i);

        MockDataHandler mdha = new MockDataHandler();
        saa.Subscribe(mdha, null);
        MockDataHandler mdhb = new MockDataHandler();
        sab.Subscribe(mdhb, null);

        Random rand = new Random();
        byte[] b = new byte[128];
        rand.NextBytes(b);
        MemBlock mb = MemBlock.Reference(b);
        sab.Send(mb);
        Assert.IsTrue(mdha.Contains(mb), "mdhb Contains " + i);

        b = new byte[128];
        rand.NextBytes(b);
        mb = MemBlock.Reference(b);
        sab.Send(mb);
        Assert.IsTrue(mdha.Contains(mb), "mdha Contains " + i);
      }

      for(int i = 250; i < 500; i++) {
        int ij = (250 % 3) + 1;
        PeerSecOverlord so = CreateInvalidSO("valid" + i, ij);
        MockSender msa = new MockSender(null, null, so, 0);
        MockSender msb = new MockSender(msa, null, so0, 0);
        msa.ReturnPath = msb;

        SecurityAssociation sab = so.CreateSecurityAssociation(msb);
        SecurityAssociation saa = so0.CreateSecurityAssociation(msa);
        Assert.AreEqual(sab.State, SecurityAssociation.States.Waiting, "sab should be waiting! " + i);
        Assert.AreEqual(saa.State, SecurityAssociation.States.Waiting, "saa should be waiting! " + i);
      }

      // create ~250 valid SAs for one guy...
      for(int i = 500; i < 750; i++) {
        PeerSecOverlord so = CreateValidSO("valid" + i);
        MockSender msa = new MockSender(null, null, so, 0);
        MockSender msb = new MockSender(msa, null, so0, 0);
        msa.ReturnPath = msb;

        SecurityAssociation sab = so.CreateSecurityAssociation(msb);
        Assert.AreEqual(sab.State, SecurityAssociation.States.Active, "sab should be active! " + i);
        SecurityAssociation saa = so0.CreateSecurityAssociation(msa);
        Assert.AreEqual(saa.State, SecurityAssociation.States.Active, "saa should be active! " + i);

        MockDataHandler mdha = new MockDataHandler();
        saa.Subscribe(mdha, null);
        MockDataHandler mdhb = new MockDataHandler();
        sab.Subscribe(mdhb, null);

        Random rand = new Random();
        byte[] b = new byte[128];
        rand.NextBytes(b);
        MemBlock mb = MemBlock.Reference(b);
        sab.Send(mb);
        Assert.IsTrue(mdha.Contains(mb), "mdhb Contains " + i);

        b = new byte[128];
        rand.NextBytes(b);
        mb = MemBlock.Reference(b);
        sab.Send(mb);
        Assert.IsTrue(mdha.Contains(mb), "mdha Contains " + i);
      }

      Random randr = new Random();
      byte[] br = new byte[128];
      randr.NextBytes(br);
      MemBlock mbr = MemBlock.Reference(br);

      // New logic requires that we call this first, to set all SAs to not
      // running, the following for loop sets all "Active" SAs back to _running
      // Thus keeping the original intent of this test.  The new logic only
      // affects testing paths.
      so0.CheckSAs(DateTime.UtcNow);

      foreach(Dictionary<ISender, PeerSecAssociation> sender_to_sa in so0.SPI.Values) {
        foreach(SecurityAssociation sa in sender_to_sa.Values) {
          if(sa.State == SecurityAssociation.States.Active) {
            sa.Send(mbr);
          }
        }
      }

      so0.CheckSAs(DateTime.UtcNow);
      Assert.AreEqual(500, so0.SACount, "Count!");

      so0.CheckSAs(DateTime.UtcNow);
      Assert.AreEqual(0, so0.SACount, "Count!");

      t.Dispose();
    }
  }
#endif
}
