/*
This program is part of BruNet, a library for the creation of efficient overlay networks.
Copyright (C) 2005  University of California
Copyright (C) 2010 P. Oscar Boykin <boykin@pobox.com>  University of Florida

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

using BT = Brunet.Transport;
using BR = Brunet.Relay;
using SCG = System.Collections.Generic;

namespace Brunet.Connections {

public interface IEdgeReplacementPolicy {

  /** Selects a ConnectionState to merge a new Connection into an old
   * @param cts the state of the ConnectionTable
   * @param c the existing Connection
   * @param c1 the ConnectionState of existing connection
   * @param c2 the ConnectionState of the new connection
   * @return c1 or c2
   */
  ConnectionState GetReplacement(ConnectionTableState cts,
                                 Connection c, ConnectionState c1, ConnectionState c2);
  
}

/** Never replace an existing edge
 */
public class NeverReplacePolicy : IEdgeReplacementPolicy {
  public static readonly NeverReplacePolicy Instance = new NeverReplacePolicy();
  public ConnectionState GetReplacement(ConnectionTableState cts,
                                 Connection c, ConnectionState c1, ConnectionState c2) {
    return c1;
  }
}

public class TypeComparer : SCG.IComparer<System.Type> {
  protected readonly SCG.List<System.Type> _ordering;
  public TypeComparer(params System.Type[] ord) {
    _ordering = new SCG.List<System.Type>(ord);
  }
  public int Compare(System.Type t1, System.Type t2) {
    int idx1 = _ordering.IndexOf(t1);
    int idx2 = _ordering.IndexOf(t2);
    if( idx1 != -1 ) {
      if( idx2 != -1 ) {
        return idx1.CompareTo(idx2);
      }
      else {
        //We don't know about 2, but we do know 1:
        return -1;
      }
    }
    else if( idx2 != -1 ) {
      //We don't know about 1, but we do know 2:
      return 1;
    }
    else {
      //Compare based on the string names:
      string s1 = t1.ToString();
      string s2 = t2.ToString();
      return s1.CompareTo(s2);
    }
  }
}

/**
 * Prefer UDP to TCP to Relay.
 * Prefer Edges that go from Highest -> Lowest address (downhill)
 */
public class DefaultERPolicy : IEdgeReplacementPolicy {

  public readonly static TypeComparer DefTC = new TypeComparer(typeof(BT.UdpEdge),
                                                               typeof(BT.TcpEdge),
                                                               typeof(BR.RelayEdge));
  protected readonly TypeComparer _tc;
  public readonly Address Local; 

  public DefaultERPolicy(Address loc) : this(loc, DefTC) { }
  public DefaultERPolicy(Address loc, TypeComparer tc) {
    Local = loc;
    _tc = tc;
  }

  public ConnectionState GetReplacement(ConnectionTableState cts,
                                 Connection c, ConnectionState c1, ConnectionState c2) {
    var e1 = c1.Edge;
    System.Type t1 = e1.GetType();
    var e2 = c2.Edge;
    System.Type t2 = e2.GetType();
    int tcmp = _tc.Compare(t1, t2);
    if( tcmp < 0 ) {
      return c1;
    }
    else if( tcmp > 0 ) {
      return c2;
    }
    else {
      /*
       * Now we are deciding between two edges of the same
       * type, and therefore, it is fundamentally arbitrary.
       * HOWEVER: both sides of the Connection should agree
       * on the computation, OR one could choose one edge,
       * and the other the opposite.
       */
      bool c1_is_down = IsDownhill(Local, c.Address, c1);
      bool c2_is_down = IsDownhill(Local, c.Address, c2);
      if( c1_is_down && (false == c2_is_down) ) {
        return c1;
      }
      if( c2_is_down && (false == c1_is_down) ) {
        return c2;
      }
      //If we get here, they are the same type, and both run in the same direction
      int e1_idx;
      int e2_idx;
      if( typeof(BT.UdpEdge).Equals(t1) ) {
        e1_idx = GetUdpIdx(c1);
        e2_idx = GetUdpIdx(c2);
      }
      else if( typeof(BR.RelayEdge).Equals(t1) ) {
        //The approach here is get unique numbers for each:
        e1_idx = GetRelayIdx(c1);
        e2_idx = GetRelayIdx(c2);
      }
      else if( typeof(BT.TcpEdge).Equals(t1) ) {
        e1_idx = GetTcpIdx(c1);
        e2_idx = GetTcpIdx(c2);
      }
      else {
        //Assume the first one added is best, and don't replace
        e1_idx = 0;
        e2_idx = 1;
      }
      //Use this number as a proxy
      return e1_idx <= e2_idx ? c1 : c2;
    }
  }
  protected int GetUdpIdx(ConnectionState cs) {
    var ue = (BT.UdpEdge)cs.Edge;
    if( ue.IsInbound ) { return ue.ID; }
    else { return ue.RemoteID; }
  }
  protected int GetRelayIdx(ConnectionState cs) {
    var ue = (BR.RelayEdge)cs.Edge;
    if( ue.IsInbound ) { return ue.LocalID; }
    else { return ue.RemoteID; }
  }
  /*
   * The idea here is to use the incoming side's view
   * of the remote|local port numbers concatenated.
   * Both sides know this information, and it should
   * uniquely determine a TCP edge (i.e. two sockets
   * can't have the same remote and local ports
   */
  protected int GetTcpIdx(ConnectionState cs) {
    var e = cs.Edge;
    BT.IPTransportAddress rta, lta;
    if( e.IsInbound ) {
      rta = (BT.IPTransportAddress)e.RemoteTA;
      lta = (BT.IPTransportAddress)e.LocalTA;
    }
    else {
      //Use the other guy:
      rta = (BT.IPTransportAddress)cs.PeerLinkMessage.Remote.FirstTA;
      lta = (BT.IPTransportAddress)cs.PeerLinkMessage.Local.FirstTA;
    }
    return (rta.Port << 16) | lta.Port;
  }

  public static bool IsDownhill(Address loc, Address rem, ConnectionState cs) {
    //our peer is local to himself:
    int cmp = loc.CompareTo(rem);
    if( cmp < 0 ) {
      return cs.Edge.IsInbound;
    }
    else if( cmp > 0 ) {
      return !cs.Edge.IsInbound;
    }
    else {
      //cmp == 0, which means we have a self connection:
      throw new System.Exception("Cannot form connection to self");
    }
  }

}


}
