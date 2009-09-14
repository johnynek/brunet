//There is no license on this file because it is so trivial
//it can be considered public domain, MIT-X11, or GPLv2 or later if
//that is convienient for licensing.

using System.Collections.Generic;

namespace Brunet.Util {
  /** A simple readonly pair class similar to std::pair in C++
   */
  public class Pair<T0,T1> {
    public readonly T0 First;
    public readonly T1 Second;
    protected int _hc;
    public Pair(T0 f, T1 s) {
      First = f;
      Second = s;
      _hc = 0;
    }
    public override bool Equals(object o) {
      if( System.Object.ReferenceEquals(o, this) ) { return true; }
      Pair<T0, T1> op = o as Pair<T0,T1>;
      if( op == null ) { return false; }
      bool first = First == null ? op.First == null : First.Equals(op.First);
      if (false == first) { return false; }       
      //else first is equal, now check second:
      return Second == null ? op.Second == null : Second.Equals(op.Second);
    }
    public override int GetHashCode() {
      if( _hc == 0 ) {
        //Make sure null doesn't map 0, else we'd recompute in that common
        //case
        _hc = (First != null ? First.GetHashCode() : -1)
            ^ (Second != null ? Second.GetHashCode() : 0 );
      }
      return _hc;
    }
    public object[] ToArray() {
      return new object[]{ First, Second };
    }
    public override string ToString() {
      return System.String.Format("Pair({0}, {1})", First, Second);
    }
  }
  
  /** A simple readonly pair class similar to std::pair in C++
   */
  public class Triple<T0,T1,T2> {
    public readonly T0 First;
    public readonly T1 Second;
    public readonly T2 Third;
    protected int _hc;
    public Triple(T0 f, T1 s, T2 t) {
      First = f;
      Second = s;
      Third = t;
      _hc = 0;
    }
    public override bool Equals(object o) {
      if( System.Object.ReferenceEquals(o, this) ) { return true; }
      Triple<T0, T1, T2> op = o as Triple<T0,T1,T2>;
      if( op == null ) { return false; }
      bool first = First == null ? op.First == null : First.Equals(op.First);
      if (false == first) { return false; }       
      //else first is equal, now check second:
      bool second = Second == null ? op.Second == null : Second.Equals(op.Second);
      if (false == second) { return false; }       
      //else second is equal, now check third:
      return Third == null ? op.Third == null : Third.Equals(op.Third);
    }
    public override int GetHashCode() {
      if( _hc == 0 ) {
        _hc = (First != null ? First.GetHashCode() : -1)
              ^ (Second != null ? Second.GetHashCode() : 0)
              ^ (Third != null ? Third.GetHashCode() : 0);
      }
      return _hc;
    }
    public object[] ToArray() {
      return new object[]{ First, Second, Third };
    }
    public override string ToString() {
      return System.String.Format("Triple({0}, {1}, {2})", First, Second, Third);
    }
  }

}
