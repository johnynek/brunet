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
using Brunet.Transport;
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
