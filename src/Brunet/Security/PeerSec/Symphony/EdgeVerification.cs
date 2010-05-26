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

using System;
using Brunet.Security.Transport;
using Brunet.Transport;

namespace Brunet.Security.PeerSec.Symphony {
  ///<summary>This provides different methods to verify Secure Edges.</summary>
  public class EdgeVerify {
    ///<summary>Verify the edge by comparing the address in the certificate to
    ///the one provided in the overlay.</summary>
    public static bool AddressInSubjectAltName(Node node, Edge e, Address addr) {
      SecureEdge se = e as SecureEdge;
      if(se == null) {
        throw new Exception("Invalid edge type!");
      }

      return se.SA.Verify(addr.ToString());
    }
  }
}
