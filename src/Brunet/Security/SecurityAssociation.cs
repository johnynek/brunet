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

using Mono.Security.X509;
using Mono.Security.X509.Extensions;
using Mono.Security.Cryptography;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;

#if BRUNET_NUNIT
using Brunet.Messaging.Mock;
using NUnit.Framework;
#endif

using Brunet;
using Brunet.Concurrent;
using Brunet.Messaging;
using Brunet.Util;
using Brunet.Transport;

namespace Brunet.Security {
  abstract public class SecurityAssociation: SimpleSource, IDataHandler,
      ISender, IWrappingSender
  {
    ///<summary>The different states for an SA to be in.</summary>
    public enum States {
      ///<summary>The SA is forming a connection but has not done so yet.</summary>
      Waiting,
      ///<summary>The SA has formed a connection and it is active.</summary>
      Active,
      ///<summary>The SA has formed a connection and is updating it.</summary>
      Updating,
      ///<summary>The SA has been closed and is no longer active.</summary>
      Closed
    }

    public delegate void StateChangeHandler(SecurityAssociation sa, States state);
    /// <summary></summary>
    public event StateChangeHandler StateChangeEvent;
#if BRUNET_NUNIT
    // This may need to be tweaked for slower machines
    public static readonly int TIMEOUT = 500;
#else
    public static readonly int TIMEOUT = 60000;
#endif
    public ISender WrappedSender { get { return Sender; } }
    ///<summary>The insecure sender we send over.</summary>
    public readonly ISender Sender;
    /// <summary></summary>
    public readonly DateTime CreationTime;
    ///<summary>The state of the SA.</summary>
    public States State { get { return _state; } }
    /// <summary></summary>
    abstract public X509Certificate LocalCertificate { get; set; }
    /// <summary></summary>
    abstract public X509Certificate RemoteCertificate { get; set; }
    ///<summary>We are closed.</summary>
    public bool Closed { get { return _closed == 1; } }

    protected CertificateHandler _ch;
    protected int _closed;
    protected bool _running;
    protected States _state;
    protected object _state_lock;

    /// <summary></summary>
    public SecurityAssociation(ISender sender, CertificateHandler ch)
    {
      Sender = sender;
      _ch = ch;
      CreationTime = DateTime.UtcNow;
      _closed = 0;
      _running = false;
      _state = States.Waiting;
      _state_lock = new object();
    }

    /// <summary></summary>
    public void CheckState()
    {
      bool running = _running;
      _running = false;
      DateTime now = DateTime.UtcNow;
#if BRUNET_NUNIT
      if(!running) {
#else
      if(!running && CreationTime.AddSeconds(TIMEOUT) < now) {
#endif
        Close("Inactivity");
      }
    }

    ///<summary>This closes the SA and cleans up its state.</summary>
    ///<returns>Returns true, if the first to call close.</summary>
    virtual public bool Close(string reason)
    {
      string string_rep = ToString();
      if(Interlocked.Exchange(ref _closed, 1) == 1) {
        return false;
      }
      UpdateState(States.Closed);
      ProtocolLog.WriteIf(ProtocolLog.Security, string_rep + " closing because " + reason);
      return true;
    }

    ///<summary>All incoming data filters through here.</summary>
    public void HandleData(MemBlock data, ISender return_path, object state)
    {
      MemBlock app_data = null;
      if(!HandleIncoming(data, out app_data)) {
        return;
      }

      Handle(app_data, this);
      _running = true;
    }

    /// <summary></summary>
    abstract protected bool HandleIncoming(MemBlock data, out MemBlock app_data);

    /// <summary></summary>
    abstract protected bool HandleOutgoing(ICopyable app_data, out ICopyable data);

    ///<summary>All outgoing data filters through here.</summary>
    public void Send(ICopyable app_data)
    {
      ICopyable data = null;
      if(!HandleOutgoing(app_data, out data)) {
        return;
      }

      Sender.Send(data);
      _running = true;
    }

    public override string ToString()
    {
      return "SecurityAssociation for " + Sender + ", State " + State;
    }

    public string ToUri()
    {
      throw new NotImplementedException();
    }

    /// <summary></summary>
    protected void UpdateState(States next)
    {
      UpdateState(_state, next, false);
    }

    protected void UpdateState(States current, States next)
    {
      UpdateState(current, next, true);
    }

    /// <summary></summary>
    protected void UpdateState(States current, States next, bool if_current)
    {
      lock(_state_lock) {
        if(if_current && _state != current) {
          return;
        } else if(_state == next || _state == States.Closed) {
          return;
        }

        _state = next;
      }

      ProtocolLog.WriteIf(ProtocolLog.Security, "Setting " + next + " on " + ToString());
      if(StateChangeEvent != null) {
        StateChangeEvent(this, next);
      }
    }

    public bool Verify(string id)
    {
      return _ch.Verify(RemoteCertificate, Sender, id);
    }
  }
}
