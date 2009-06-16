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
using Brunet.Security;
using System;

namespace Brunet.Security.Transport {
  ///<summary>Binds an EdgeListener to the SecurityOverlord and handles the
  ///securing of insecure edges.  This class is thread-safe.</summary>
  public class SecureEdgeListener: WrapperEdgeListener {
    protected SecurityOverlord _so;

    ///<summary>This is the default Edge Security Parameters index.</summary>
    public static readonly int DefaultEdgeSPI = 432145;

    ///<summary>This is the default Edge Security Parameters.</summary>
    static SecureEdgeListener() {
      new SecurityPolicy(432145, "NullEncryption", "HMACSHA1");
    }

    public SecureEdgeListener(EdgeListener el, SecurityOverlord so): base(el) {
      _so = so;
      _so.AnnounceSA += AnnounceSA;
    }

    ///<summary>Makes the SecurityOverlord listen to the edge and instantiates
    ///a new SecurityAssociation for the insecure edge.  CreateSecurityAssociation
    ///is idempotent.</summary>
    protected override void WrapEdge(Edge edge) {
      edge.Subscribe(_so, null);
      SecurityAssociation sa = _so.CreateSecurityAssociation(edge, DefaultEdgeSPI, !edge.IsInbound);
      if(edge.IsClosed) {
        sa.Close("Edge closed too quickly.");
      }
    }

    ///<summary>When a SecurityAssociation changes amongst inactive, active,
    ///or closed this gets notified.</summary>
    protected void AnnounceSA(object o, EventArgs ea) {
      SecurityAssociation sa = o as SecurityAssociation;
      if(sa == null) {
        throw new Exception("Needs to be a SecurityAssociation");
      }

      Edge e = sa.Sender as Edge;
      // if e is an edge, let's see if he's creating a SE
      // or maybe it isn't our edge!
      if(e == null) {
        return;
      } else if(e.TAType != this.TAType) {
        return;
      }

      if(sa.Active) {
        SecureEdge se = new SecureEdge(e, sa);
        sa.Subscribe(se, null);
        try {
          Finalize(se);
        } catch {
          se.Close();
        }
      } else {
        e.Close();
      }
    }
  }
}
