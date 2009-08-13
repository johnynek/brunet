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
using Brunet.Security.Transport;

namespace Brunet.Security.Protocol {
  ///<summary>This provides different methods to verify Secure Edges.</summary>
  public class EdgeVerify {
    ///<summary>Verify the edge by comparing the address in the certificate to
    ///the one provided in the overlay.</summary>
    public static bool AddressInSubjectAltName(Node node, Edge e, Address addr) {
      SecureEdge se = e as SecureEdge;
      if(se == null) {
        throw new Exception("Invalid edge type!");
      }

      return se.SA.VerifyCertificateBySubjectAltName(addr.ToString());
    }
  }
}
