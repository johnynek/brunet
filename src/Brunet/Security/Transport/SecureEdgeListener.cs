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
using System.Collections.Generic;

namespace Brunet.Security.Transport {
  ///<summary>Binds an EdgeListener to the SecurityOverlord and handles the
  ///securing of insecure edges.  This class is thread-safe.</summary>
  public class SecureEdgeListener: WrapperEdgeListener {
    protected SecurityOverlord _so;
    protected Dictionary<Edge, SecurityAssociation> _edge_to_sa;
    protected Dictionary<Edge, bool> _edge_to_inbound;

    public SecureEdgeListener(EdgeListener el, SecurityOverlord so): base(el) {
      _so = so;
      _so.AnnounceSA += AnnounceSA;
      _edge_to_sa = new Dictionary<Edge, SecurityAssociation>();
      _edge_to_inbound = new Dictionary<Edge, bool>();
    }

    ///<summary>Makes the SecurityOverlord listen to the edge and instantiates
    ///a new SecurityAssociation for the insecure edge.  CreateSecurityAssociation
    ///is idempotent.</summary>
    protected override void WrapEdge(Edge edge) {
      edge.Subscribe(_so, null);

      if(edge.IsInbound) {
        lock(_sync) {
          _edge_to_inbound.Add(edge, true);
        }
      } else {
        SecurityAssociation sa = _so.CreateSecurityAssociation(edge);
        lock(_sync) {
          _edge_to_sa.Add(edge, sa);
        }
      }

      try {
        edge.CloseEvent += HandleEdgeClose;
      } catch {
        HandleEdgeClose(edge, EventArgs.Empty);
      }
    }

    protected void HandleEdgeClose(object edge, EventArgs ea) {
      Edge e = edge as Edge;
      if(e == null) {
        throw new Exception("Should be Edge");
      }

      SecurityAssociation sa = null;
      RemoveFromDictionary(e, out sa);

      if(sa != null) {
        sa.Close("Edge closed early.");
      }
    }

    protected bool RemoveFromDictionary(Edge edge, out SecurityAssociation sa) {
      bool found = false;

      lock(_sync) {
        if(_edge_to_sa.TryGetValue(edge, out sa)) {
          _edge_to_sa.Remove(edge);
          found = true;
        } else if(_edge_to_inbound.ContainsKey(edge)) {
          _edge_to_inbound.Remove(edge);
          found = true;
        }
      }
      return found;
    }
      

    ///<summary>When a SecurityAssociation changes amongst inactive, active,
    ///or closed this gets notified.</summary>
    protected void AnnounceSA(SecurityAssociation sa,
        SecurityAssociation.States state)
    {
      Edge e = sa.Sender as Edge;
      // if e is an edge, let's see if he's creating a SE
      // or maybe it isn't our edge!
      if(e == null) {
        return;
      } else if(e.TAType != this.TAType) {
        return;
      }

      if(state == SecurityAssociation.States.Active) {
        SecurityAssociation stored_sa = null;
        if(!RemoveFromDictionary(e, out stored_sa)) {
          // May have already been here
          return;
        } else if(stored_sa != null && stored_sa != sa) {
          throw new Exception("Cannot have the same edge used in multiple SAs");
        }

        SecureEdge se = new SecureEdge(e, sa);
        sa.Subscribe(se, null);
        try {
          Finalize(se);
        } catch {
          se.Close();
        }
      } else if(state == SecurityAssociation.States.Closed) {
        e.Close();
      }
    }
  }
}
