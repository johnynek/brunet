/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida
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
    public void BeginFindingTAs()
    {
      if(Interlocked.Exchange(ref _running, 1) == 1) {
        return;
      }

      _fe = Brunet.Util.FuzzyTimer.Instance.DoEvery(SeekTAs, DELAY_MS, DELAY_MS / 2);
      // If we don't execute this now, the first one won't be called for DELAY_MS
      SeekTAs(DateTime.UtcNow);
    }

    /// <summary>No more TAs are necessary.</summary>
    public void EndFindingTAs()
    {
      FuzzyEvent fe = _fe;
      if(Interlocked.Exchange(ref _running, 0) == 0) {
        return;
      }

      if(fe != null) {
        fe.TryCancel();
      }
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
      _ta_handler.UpdateRemoteTAs(tas);
    }

    /// <summary>Called to inquire the shared medium for TAs.</summary>
    abstract protected void SeekTAs(DateTime now);
  }
}
