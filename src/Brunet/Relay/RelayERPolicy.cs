/*
Copyright (C) 2010 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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


using Brunet.Connections;
namespace Brunet.Relay {

  public class RelayERPolicy : IEdgeReplacementPolicy {
    protected readonly IEdgeReplacementPolicy _fallback;
    static public readonly RelayERPolicy Instance = new RelayERPolicy();
    private RelayERPolicy() : this(NeverReplacePolicy.Instance) { }
    public RelayERPolicy(IEdgeReplacementPolicy fallback) {
      if( null == fallback ) {
        throw new System.ArgumentNullException("fallback IEdgeReplacementPolicy cannot be null");
      }
      _fallback = fallback;
    }

    public ConnectionState GetReplacement(ConnectionTableState cts,
                                 Connection c, ConnectionState c1,
                                 ConnectionState c2) {
      if( (c1.Edge is RelayEdge) && (c2.Edge is RelayEdge) ) {
        return GetIdx(c1) <= GetIdx(c2) ? c1 : c2;
      }
      return _fallback.GetReplacement(cts,c,c1,c2);
    }
    protected int GetIdx(ConnectionState cs) {
      var ue = (RelayEdge)cs.Edge;
      if( ue.IsInbound ) { return ue.LocalID; }
      else { return ue.RemoteID; }
    }
  }
}
