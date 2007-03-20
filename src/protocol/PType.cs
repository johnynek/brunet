/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet {

#if BRUNET_NUNIT
[TestFixture]
#endif

/**
 * This is a class the parses and represents types for packets/payloads
 * in Brunet.
 */
public class PType : ICopyable {
  
  protected int _type_num;
  protected MemBlock _raw_data;
  protected string _string_rep;

  /**
   * packet numbers 1-31 are allowed
   */
  public PType(int number) {
    if( !IsValidNumeric(number) ) { throw new System.Exception("Type numbers must be > 0 and <= 31"); }
    _type_num = number;
  }
  /**
   * Initialize a packet type from a string
   */
  public PType(string s) {
    _string_rep = s;
    _type_num = -2;
  }
#if BRUNET_NUNIT
  //Only used for NUnit testing, don't EVER use this
  public PType() : this(1) { }
#else
  /**
   * Used by the Parse method
   */
  protected PType() {

  }
#endif

  ///For ICopyable support
  public int Length { get { return ToMemBlock().Length; } }
  public int CopyTo(byte[] buf, int off) { return ToMemBlock().CopyTo(buf, off); }

  public override int GetHashCode() { return ToMemBlock().GetHashCode(); }
  public override bool Equals(object o) {
    if( o == this ) { return true; }
    PType other = o as PType;
    if( other != null ) {
      return other.ToMemBlock().Equals( ToMemBlock() );
    }
    return false;
  }

  /**
   * Parse the PType starting at mb, and return all of mb <b>after</b>
   * the PType.
   */
  static PType Parse(MemBlock mb, out MemBlock rest) {
    MemBlock raw_data = null;
    PType result = new PType();
    if( IsValidNumeric( (int)( mb[0] ) ) ) {
      raw_data = mb.Slice(0,1);
      rest = mb.Slice(1);
      result._type_num = (int)mb[0];
    }
    else {
      int null_pos = mb.IndexOf(0);
      if( null_pos > 0 ) {
        //Include the "null"
        raw_data = mb.Slice(0, null_pos + 1);
        rest = mb.Slice(null_pos + 1); 
      }
      else {
        //There is no terminating Null, panic!!
        throw new System.Exception("PType not null terminated");
      }
      result._type_num = -2;
    }
    result._raw_data = raw_data;
    return result;
  }

  public int TypeNumber {
    get {
      if( _type_num == -2 ) {
        //We haven't computed it yet:

        //Ignore the trailing 0:
        MemBlock mb = ToMemBlock();
        int l = mb.Length - 1;
        if( l < 4 ) {
          int t = 0;
          for(int i = 0; i < l; i++) {
            t = t | mb[i];
            t <<= 8;
          }
          _type_num = t;
        }
        else {
          //Too big, oh well
          _type_num = -1;
        }
      }
      return _type_num;
    }
  }

  /**
   * @return true if this number can be stored as a 1 byte PType
   */
  public static bool IsValidNumeric(int number) {
    return ( 0 < number && number < 32 );
  }

  public MemBlock ToMemBlock() {
    if( _raw_data != null ) { return _raw_data; }
    //Else make it:
    if( IsValidNumeric( _type_num ) ) {
      byte[] buf = new byte[1];
      buf[0] = (byte)_type_num;
      _raw_data = MemBlock.Reference(buf);
    }
    else {
      //It's a string type:
      int l = NumberSerializer.GetByteCount(_string_rep);
      byte[] buf = new byte[l];
      NumberSerializer.WriteString(_string_rep, buf, 0);
      _raw_data = MemBlock.Reference(buf);
    }
    return _raw_data;
  }

  public override string ToString() {
    if( _string_rep != null ) {
      return _string_rep;
    }
    else if ( _type_num == -2 ) {
      //Unitialized string type:
      _string_rep = _raw_data.Slice(0, _raw_data.Length - 1).GetString(System.Text.Encoding.UTF8);
    }
    else {
      //Unitialized int type:
      _string_rep = "_" + _type_num.ToString();
    }
    return _string_rep;
  }

#if BRUNET_NUNIT
  [Test]
  public void Test() {
    
    System.Random r = new System.Random();
    //Test numeric type codes:
    for(int i = 1; i < 32; i++ ) {
      PType p = new PType(i);
      MemBlock b = p.ToMemBlock();
      
      byte[] buf = new byte[100];
      r.NextBytes(buf); //Get some junk:
      MemBlock junk = MemBlock.Reference(buf);
      MemBlock b1 = MemBlock.Concat(b, junk);
      MemBlock rest = null;
      PType pp = PType.Parse(b1, out rest);
      
      byte[] buf2 = new byte[1];
      buf2[0] = (byte)i;
      MemBlock b2 = MemBlock.Reference(buf2);
      
      Assert.AreEqual(p, pp, System.String.Format("Round trip int: {0}", i));
      Assert.AreEqual( b, b2, System.String.Format("Convert to MemBlock int: {0}", i) );
      Assert.AreEqual(i, pp.TypeNumber, "Typenumber equality");
      Assert.AreEqual(rest, junk, "rest in int PType");
    }

    //Test string types:
    for(int i = 0; i < 1000; i++) {
      //Make a random string:
      //
      byte[] buf = new byte[ r.Next(1, 100) ];
      r.NextBytes(buf);
      string s = Base32.Encode(buf);
      PType p1 = new PType(s);
      r.NextBytes(buf); //Get some junk:
      MemBlock b = MemBlock.Copy(buf);
      MemBlock combine = MemBlock.Concat( p1.ToMemBlock(), b);
      MemBlock b2 = null;
      PType p2 = PType.Parse(combine, out b2);

      Assert.AreEqual( p1, p2, "Round trip string: " + s);
      Assert.AreEqual( b, b2, "Round trip rest" );
      Assert.AreEqual( s, p2.ToString(), "Round trip to string");
      Assert.AreEqual( s, p1.ToString(), "Round trip to string");
      Assert.AreEqual( p1.TypeNumber, p2.TypeNumber, "RT: TypeNumber test");
    }
    //Test TypeNumber of string types:
    for(int i = 0; i < 100; i++) {
      byte[] buf = new byte[20];
      r.NextBytes(buf);
      for( int j = 1; j < 4; j++) {
        string s = Base32.Encode(buf).Substring(0, j);
        PType p1 = new PType(s);
        byte[] buf2 = System.Text.Encoding.UTF8.GetBytes(s);
        int t = 0;
        for(int k = 0; k < buf2.Length; k++) {
          t = t | buf2[k];
          t <<= 8;
        }
        Assert.AreEqual(t, p1.TypeNumber, System.String.Format("String type number: {0}, s={1}", t, s) );
      }
    }
    //System.Console.WriteLine("Tested PType");
  }
#endif
}

}
