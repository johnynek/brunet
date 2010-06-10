/*
Copyright (C) 2010  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using Brunet.Concurrent;
using Brunet.Transport;
using Brunet.Util;
using Mono.Security.X509;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Security.Cryptography;

#if BRUNET_NUNIT
using Brunet.Messaging.Mock;
using NUnit.Framework;
#endif

using Brunet.Messaging;
namespace Brunet.Security {
  /// <summary></summary>
  abstract public class SecurityOverlord : SimpleSource, IDataHandler {
    ///<summary>Called when an SA has switched between active and not.</summary>
    public event SecurityAssociation.StateChangeHandler AnnounceSA;
    /// <summary>Allows user code to interact with the CertificateHandler given the
    /// SecurityOverlord</summary>
    public CertificateHandler CertificateHandler { get { return _ch; } } 
    abstract public int SACount { get; }

    protected readonly RSACryptoServiceProvider _private_key;
    protected readonly CertificateHandler _ch;
    protected readonly FuzzyEvent _sa_check;
    protected int _stopped;

    abstract protected IEnumerable<SecurityAssociation> _sas { get; }

    public const int CHECK_SA_PERIOD = 300000;

    /// <summary></summary>
    public SecurityOverlord(RSACryptoServiceProvider private_key,
        CertificateHandler ch)
    {
      _private_key = private_key;
      _ch = ch;
      _stopped = 0;
      _sa_check = FuzzyTimer.Instance.DoEvery(CheckSAs, CHECK_SA_PERIOD, CHECK_SA_PERIOD / 2);
    }

#if BRUNET_NUNIT
    public void CheckSAs(DateTime now)
#else
    /// <summary>All SAs check their state for use, closing themselves if there
    /// has been considerable delay since last use or issuing a renegotiation
    /// after excessive use.</summary>
    protected void CheckSAs(DateTime now)
#endif
    {
      foreach(SecurityAssociation sa in _sas) {
        sa.CheckState();
      }
    }

    /// <summary></summary>
    protected void SACloseHandler(object o, EventArgs ea)
    {
      SecurityAssociation sa = o as SecurityAssociation;
      if(sa == null) {
        throw new Exception("Object should be a SecurityAssociation!");
      }
      RemoveSA(sa);
    }

    /// <summary></summary>
    public void Stop()
    {
      if(Interlocked.Exchange(ref _stopped, 1) == 1) {
        return;
      }

      _sa_check.TryCancel();

      foreach(SecurityAssociation sa in _sas) {
        sa.Close("Shutting down...");
      }
    }

    /// <summary>Checks with the CertificateHandler to see if the certificates
    /// for active sessions are still valid.  If they are not, they are closed
    /// immediately.</summary>
    public void VerifySAs()
    {
      foreach(SecurityAssociation sa in _sas) {
        string message = "Certificate revoked.";
        bool valid = false;

        try {
          valid = _ch.Verify(sa.RemoteCertificate, sa.Sender);
        } catch(Exception e) {
          message = e.Message;
        }

        if(!valid) {
          try {
            sa.Close(message);
          } catch { }
        }
      }
    }

    /// <summary></summary>
    public SecurityAssociation CreateSecurityAssociation(ISender sender)
    {
      return CreateSecurityAssociation(sender, true);
    }

    /// <summary>Whenever an SA changes amongst inactive, active, and closed
    /// this is called.</summary>
    /// <param name="o">The SA whose state changes.</summary>
    protected void SAStateChange(SecurityAssociation sa,
        SecurityAssociation.States state)
    {
      if(state == SecurityAssociation.States.Active) {
        if(_sub != null) {
          sa.Subscribe(_sub.Handler, null);
        } else {
          sa.Subscribe(this, null);
        }
      } else if(state == SecurityAssociation.States.Updating) {
        sa.Subscribe(this, null);
      } else if(sa.Closed) {
        RemoveSA(sa);
      }

      if(AnnounceSA != null) {
        AnnounceSA(sa, state);
      }
    }

    /// <summary></summary>
    abstract public SecurityAssociation CreateSecurityAssociation(ISender sender, bool start);
    /// <summary></summary>
    abstract public void HandleData(MemBlock data, ISender return_path, object state);
    /// <summary></summary>
    abstract protected void RemoveSA(SecurityAssociation sa);
  }
}
