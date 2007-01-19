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

/**
 * This class wraps a byte array so we can do useful operations
 * with it, but not change it.  A MemBlock is immutable.
 */
#if BRUNET_NUNIT
[TestFixture]
#endif
public class MemBlock : System.IComparable {

  protected byte[] _buffer;
  protected int _offset;
  protected int _length;
  //The number of bytes in this MemBlock
  public int Length { get { return _length; } }

  /**
   * Allow us to read from the MemBlock
   */
  public byte this[int pos] {
    get {
      return _buffer[ _offset + pos ];
    }
  }
  /**
   * This DOES NOT MAKE A COPY.  If you change the data byte array
   * after creating this object you are in for trouble.  It is your
   * responsibility to copy the data if you plan to overwrite data
   * @see MemBlock.Copy
   * @see MemBlock.Reference
   */
  protected MemBlock(byte[] data, int offset, int length) {
    _buffer = data;
    _offset = offset;
    _length = length;
    if( data.Length - offset < length ) {
      //This does not make sense:
      throw new System.Exception("byte array not long enough");
    }
  }
  /**
   * Here is the null MemBlock
   */
  public MemBlock() {
    _buffer = null;
    _offset = 0;
    _length = 0;
  }

  /**
   * Shorter MemBlocks are less than longer ones.  MemBlocks of identical
   * length are compared from first byte to last byte.  The first byte
   * that differs is compared to get the result of the function
   */
  public int CompareTo(object o) {
    MemBlock other = o as MemBlock;
    if ( other == null ) {
      //Put us ahead of all other types, this might not be smart
      return -1;
    }
    int t_l = this.Length;
    int o_l = other.Length;
    if( t_l == o_l ) {
      for(int i = 0; i < t_l; i++) {
        byte t_b = this[i];
        byte o_b = other[i];
        if( t_b < o_b ) {
          return -1;
        }
        else if( t_b > o_b ) {
          return 1;
        }
      }
      //We must be equal
      return 0;
    }
    else if( t_l < o_l ) {
      return -1;
    }
    else {
      return 1;
    }
  }

  /**
   * Concatenate the given MemBlock objects into one
   */
  static public MemBlock Concat(params MemBlock[] blocks) {
    int total_length = 0;
    foreach(MemBlock mb in blocks) {
      total_length += mb.Length;
    }
    byte[] buffer = new byte[ total_length ];

    int offset = 0;
    foreach(MemBlock mb in blocks) {
      offset += mb.CopyTo(buffer, offset);
    }
    return new MemBlock(buffer, 0, total_length);
  }

  /**
   * Copy the entire contents of the MemBlock into an Array starting
   * at the given offset.  If you want to only copy a range, Splice
   * a smaller MemBlock first, and use that.
   * @param dest the destination for the copy
   * @param offset_into_dest the offset to start at in dest
   * @return the number of bytes copied
   */
  public int CopyTo(byte[] dest, int offset_into_dest) {
    System.Array.Copy(_buffer, _offset, dest, offset_into_dest, _length);
    return _length;
  }

  /**
   * Make a copy of the source and return it as a MemBlock
   * Note that the constructor does not make a copy.
   * @param source where to copy from
   * @param offset the offset into the source to start copying
   * @param length the number of bytes to copy
   * @return a MemBlock holding a copy to this byte array
   */
  public static MemBlock Copy(byte[] source, int offset, int length) {
    byte[] buffer = new byte[length];
    System.Array.Copy(source, offset, buffer, 0, length);
    return new MemBlock(buffer, 0, length);
  }
  
  public override bool Equals(object a) {
    return (this.CompareTo(a) == 0);
  }

  //Uses the first few bytes as the hashcode
  public override int GetHashCode() {
    int l = this.Length;
    if( l > 3 ) {
      return NumberSerializer.ReadInt(_buffer, _offset);
    }
    if( l > 1 ) {
      return (int)NumberSerializer.ReadShort(_buffer, _offset);
    }
    if( l == 1 ) { return (int)_buffer[_offset]; }
    
    return 0;
  }
  /**
   * Make a reference to the given byte array, it does not make a copy.
   * This is used rather than a constructor to make it obvious to the
   * caller that this is only making a reference, not a copy
   */
  static public MemBlock Reference(byte[] data, int offset, int length) {
    return new MemBlock(data, offset, length);
  }
  /**
   * Returns a new MemBlock which starts at a given offset in the
   * current block and runs a given total length
   */
  public MemBlock Splice(int offset, int length) {
    return new MemBlock(_buffer, _offset + offset, length);
  }
  
  /**
   * Return the "tail" of the current MemBlock starting at a given
   * offset.
   */
  public MemBlock Splice(int offset) {
    return new MemBlock(_buffer, _offset + offset, _length - offset);
  }

#if BRUNET_NUNIT
  [Test]
  public void Test() {
    System.Random r = new System.Random();

    byte[] data;
    for(int i = 0; i < 100; i++) {
      data = new byte[ r.Next(1024) ];
      r.NextBytes(data);
      int offset = r.Next(data.Length);
      MemBlock mb1 = new MemBlock(data, 0, data.Length);
      MemBlock mb1a = MemBlock.Copy(data, 0, data.Length);
      Assert.AreEqual(mb1, mb1a, "MemBlock.Copy");
      MemBlock mb2 = new MemBlock(data, offset, data.Length - offset);
      MemBlock mb2a = mb1.Splice(offset);
      MemBlock mb3 = new MemBlock(data, 0, offset);
      MemBlock mb3a = mb1.Splice(0, offset);
      Assert.IsTrue(mb3.Equals( mb3a ), "mb3.Equals(mb3a)");
      Assert.IsTrue(mb3a.Equals( mb3 ), "mb3a.Equals(mb3)");
      Assert.AreEqual(mb3.CompareTo(mb2) + mb2.CompareTo(mb3), 0, "CompareTo");
      Assert.IsTrue(mb2.Equals( mb2a ), "mb2.Equals(mb2a)");
      Assert.IsTrue(mb2a.Equals( mb2 ), "mb2a.Equals(mb2)");

      MemBlock cat = MemBlock.Concat(mb3, mb2);
      MemBlock cata = MemBlock.Concat(mb3a, mb2a);
      Assert.IsTrue(cat.Equals(cata), "Concat Equals");
      Assert.IsTrue(cata.Equals(cat), "Concat a Equals");
      Assert.IsTrue(mb1.Equals(cat), "Concat Equals Original");
      if( offset != 0 ) {
        //These should not be equal
        Assert.IsFalse(mb2.Equals(mb1), "mb2 != mb1");
      }
      int mb2a_l = mb2a.Length;
      byte[] tmp_data = new byte[mb2a_l];
      mb2a.CopyTo(tmp_data, 0);
      MemBlock mb2b = new MemBlock(tmp_data, 0, tmp_data.Length);
      Assert.IsTrue(mb2a.Equals(mb2b), "mb2a.Equals(mb2b)");
      Assert.IsTrue(mb2b.Equals(mb2a), "mb2b.Equals(mb2a)");

      //Check the Hash:
      Assert.AreEqual(mb2b.GetHashCode(), mb2a.GetHashCode(), "GetHashCode");

      //Here are some manual equality testing using the indexer
      bool all_equals = true;
      int j = 0;
      while( all_equals && (j < mb1.Length) ) {
        all_equals = (mb1[ j ] == cat[ j ]);
        j++;
      }
      Assert.IsTrue(all_equals, "Manual equality test mb1");
      all_equals = true;
      j = 0;
      while( all_equals && (j < mb2.Length) ) {
        all_equals = (mb2[ j ] == mb2a[ j ]);
        j++;
      }
      Assert.IsTrue(all_equals, "Manual equality test mb2");
      all_equals = true;
      j = 0;
      while( all_equals && (j < mb2.Length) ) {
        all_equals = (mb2[ j ] == mb2b[ j ]);
        j++;
      }
      Assert.IsTrue(all_equals, "Manual equality test mb2b");
    }
  }

#endif
}

}
