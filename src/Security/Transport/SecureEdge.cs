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

using System;
using System.Threading;

namespace Brunet {
  ///<summary>Provides a Security Wrapper for generic Edges.  Basically binds a
  ///SecurityAssociation to an Edge, so that all traffic must traverse the
  ///SecurityAssociation.</summary>
  public class SecureEdge: WrapperEdge, IDataHandler {
    ///<summary>The Security provider.</summary>
    public readonly SecurityAssociation SA;
    protected int _closed;

    public SecureEdge(Edge edge, SecurityAssociation sa):
      base(edge, false)
    {
      SA = sa;
      _closed = 0;
    }

    ///<summary>Pushes the data to the SecurityAssociation who will send it
    ///over the underlying edge.</summary>
    public override void Send(ICopyable data) {
      if(_closed == 1) {
        throw new EdgeClosedException("SecureEdge has been closed.");
      }
      try {
        SA.Send(data);
      } catch(Exception e) {
        throw new EdgeException(_closed == 1, "Unable to send on SE", e);
      }
    }

    public override bool Close() {
      if(Interlocked.Exchange(ref _closed, 1) == 1) {
        return false;
      }
      SA.Close("Edge closed!");
      base.Close();
      return true;
    }

    public override string ToString() {
      return "SecureEdge: " + _edge.ToString();
    }
  }
}
