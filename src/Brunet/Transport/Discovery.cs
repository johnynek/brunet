/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida
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

using Brunet.Util;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Brunet.Transport {
  /// <summary>This class attempts to provide mechanisms for dealing with the
  /// bootstrap problem: how do we find peers in our overlay using some existing
  /// shared medium.</summary>
  public abstract class Discovery {
    public const int DELAY_MS = 10000;
    protected FuzzyEvent _fe;
    protected int _running;
    protected ITAHandler _ta_handler;

    public Discovery(ITAHandler ta_handler)
    {
      Interlocked.Exchange(ref _running, 0);
      _ta_handler = ta_handler;
    }

    /// <summary>We need some TAs.</summary>
    virtual public bool BeginFindingTAs()
    {
      if(Interlocked.Exchange(ref _running, 1) == 1) {
        return false;
      }

      _fe = Brunet.Util.FuzzyTimer.Instance.DoEvery(SeekTAs, DELAY_MS, DELAY_MS / 2);
      // If we don't execute this now, the first one won't be called for DELAY_MS
      SeekTAs(DateTime.UtcNow);
      return true;
    }

    /// <summary>No more TAs are necessary.</summary>
    virtual public bool EndFindingTAs()
    {
      FuzzyEvent fe = _fe;
      if(Interlocked.Exchange(ref _running, 0) == 0) {
        return false;
      }

      if(fe != null) {
        fe.TryCancel();
      }
      return true;
    }

    virtual public bool Stop()
    {
      return EndFindingTAs();
    }

    /// <summary>Returns a list of all local TAs as strings.</summary>
    protected IList LocalTAsToString()
    {
      return LocalTAsToString(-1);
    }

    /// <summary>Returns a list of some local TAs as strings.</summary>
    protected IList LocalTAsToString(int max_tas)
    {
      var tas = _ta_handler.LocalTAs;
      max_tas = (max_tas < 0) ? tas.Count : max_tas;
      max_tas = Math.Min(max_tas, tas.Count);
      ArrayList tas_as_str = new ArrayList(max_tas);

      for(int i = 0; i < max_tas; i++) {
        tas_as_str.Add(tas[i].ToString());
      }
      return tas_as_str;
    }

    /// <summary>Converts a list of TAs as strings into TA objects.</summary>
    protected void UpdateRemoteTAs(IList tas_as_str)
    {
      var tas = new List<TransportAddress>(tas_as_str.Count);
      foreach(string ta in tas_as_str) {
        try {
          tas.Add(TransportAddressFactory.CreateInstance(ta));
        } catch (Exception e) {
          ProtocolLog.WriteIf(ProtocolLog.Exceptions, "Unexpected exception: " + e);
        }
      }
      UpdateRemoteTAs(tas);
    }

    protected void UpdateRemoteTAs(List<TransportAddress> tas)
    {
      _ta_handler.UpdateRemoteTAs(tas);
    }

    /// <summary>Called to inquire the shared medium for TAs.</summary>
    abstract protected void SeekTAs(DateTime now);
  }
}
