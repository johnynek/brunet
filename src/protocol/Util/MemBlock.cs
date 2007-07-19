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
public class MemBlock : System.IComparable, System.ICloneable, Brunet.ICopyable {

  protected readonly byte[] _buffer;
  protected readonly int _offset;
  protected readonly int _length;
  //The number of bytes in this MemBlock
  public int Length { get { return _length; } }

  /**
   * As long as this MemBlock is not garbage collected,
   * it is keeping an underlying buffer from being garbage
   * collected.  This is how big that buffer is.  We might
   * want to make a copy of some data we keep if it is keeping
   * a large buffer from being collected
   */
  public int ReferencedBufferLength {
    get {
      if( _buffer == null ) {
        return 0;
      }
      else {
        return _buffer.Length;
      }
    }
  }

  protected static readonly MemBlock _null = new MemBlock(null, 0, 0);
  /**
   * Here is a length == 0 MemBlock, which can be useful in some
   * cases
   */
  public static MemBlock Null { get { return _null; } }
  /**
   * Allow us to read from the MemBlock
   */
  public byte this[int pos] {
    get {
      if( pos >= _length ) {
        throw new System.ArgumentOutOfRangeException("pos", pos,
                                          "Position greater than MemBlock length");
      }
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
    if( length == 0 ) {
      //Make sure not to keep a reference, which could keep memory in scope
      _buffer = null;
      _offset = 0;
    }
    else if ( length < 0 ) {
      throw new System.ArgumentOutOfRangeException("length", length,
                                          "MemBlock cannot have negative length");
    }
    else if( data.Length < offset + length ) {
      /*
       * Clearly length > 0 otherwise one of the above two conditions
       * would be true
       */
      throw new System.ArgumentException("byte array not long enough");
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
   * Allow conversion to byte[] by making a copy.
   * This allows us to pass MemBlock as if they were byte[] objects
   */
  public static implicit operator byte[](MemBlock b) {
    byte[] result = new byte[ b.Length ];
    b.CopyTo(result, 0);
    return result;
  }

  /**
   * Allow implicit conversion from byte[] by making a reference to the
   * original byte[] , if that byte[] is changed, we're screwed.
   * This allows us to pass byte[] objects as if they were MemBlock
   */
  public static implicit operator MemBlock(byte[] data) {
    return Reference(data);
  }

  /**
   * Implements ICloneable.  This copies the underlying buffer.
   * Might be useful if you want to keep something around that
   * would otherwise prevent a large amount of memory from being
   * GC'ed
   */
  public object Clone() {
    return Copy(_buffer, _offset, _length);
  }
  /**
   * Shorter MemBlocks are less than longer ones.  MemBlocks of identical
   * length are compared from first byte to last byte.  The first byte
   * that differs is compared to get the result of the function
   */
  public int CompareTo(object o) {
    if( this == o ) { return 0; }
    MemBlock other = o as MemBlock;
    if ( other == null ) {
      byte[] data = o as byte[];
      if( data != null ) {
        other = MemBlock.Reference(data);
      }
      else {
        //Put us ahead of all other types, this might not be smart
        return -1;
      }
    }
    int t_l = this.Length;
    int o_l = other.Length;
    if( t_l == o_l ) {
      for(int i = 0; i < t_l; i++) {
        byte t_b = this._buffer[ this._offset + i];
        byte o_b = other._buffer[ other._offset + i];
        if( t_b != o_b ) {
          //OKAY! They are different:
          if( t_b < o_b ) {
            return -1;
          }
          else {
            return 1;
          }
        }
        else {
          //This position is equal, go to the next
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
   * Concatenate the given ICopyable objects into one MemBlock.
   * @see CopySet if you don't want to do the copy now
   */
  static public MemBlock Concat(params ICopyable[] blocks) {
    int total_length = 0;
    foreach(ICopyable mb in blocks) {
      total_length += mb.Length;
    }
    byte[] buffer = new byte[ total_length ];

    int offset = 0;
    foreach(ICopyable mb in blocks) {
      offset += mb.CopyTo(buffer, offset);
    }
    return new MemBlock(buffer, 0, total_length);
  }

  /**
   * Copy the entire contents of the MemBlock into an Array starting
   * at the given offset.  If you want to only copy a range, Slice
   * a smaller MemBlock first, and use that.
   * @param dest the destination for the copy
   * @param offset_into_dest the offset to start at in dest
   * @return the number of bytes copied
   */
  public int CopyTo(byte[] dest, int offset_into_dest) {
    if( _length != 0 ) {
      System.Array.Copy(_buffer, _offset, dest, offset_into_dest, _length);
    }
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
  /**
   * Same as the above except offset is zero and we copy the whole lenght
   */
  public static MemBlock Copy(byte[] source) {
    return Copy(source, 0, source.Length);
  }
  /**
   * Copy an ICopyable object into a MemBlock.  You might do this
   * if you wanted to access the i^th byte, something you can't do with
   * ICopyable
   */
  public static MemBlock Copy(ICopyable c) {
    int l = c.Length;
    if( l != 0 ) {
      byte[] buffer = new byte[l];
      c.CopyTo(buffer, 0);
      return new MemBlock(buffer, 0, buffer.Length);
    }
    else {
      return _null;
    }
  }
  
  public override bool Equals(object a) {
    if (this == a) {
      //Clearly we are the Equal to ourselves
      return true;
    }
    if( a is byte[] ) {
      /** 
       * @todo
       * This is very questionable to just treat byte[] as MemBlock,
       * because the hashcodes won't be equal, but we have code
       * that does this.  We should remove the assumption that MemBlock
       * can equal a byte[]
       */
      a = MemBlock.Reference((byte[])a);
    }
    if( this.GetHashCode() != a.GetHashCode() ) {
      //Hashcodes must be equal for the objects to be equal
      return false;
    }
    else {
      return (this.CompareTo(a) == 0);
    }
  }
  /**
   * This is a risky method.  It moves the initial offset down by
   * count, and returns a MemBlock with that.  Who knows
   * what may be in that data, but if you could check before you use 
   * it (useful for "unslicing" cases where one or two bytes might have
   * been sliced off
   */
  public MemBlock ExtendHead(int count) {
    return new MemBlock(_buffer, _offset - count, _length + count);
  }
  /*
   * We only calculate the hash code once (when we first need it)
   * We can use this to help us make Equals faster
   */
  protected volatile bool _have_hc = false;
  protected volatile int _hc;
  //Uses the first few bytes as the hashcode
  public override int GetHashCode() {
    if( _have_hc ) {
      return _hc;
    }

    //Use at most 4 bytes:
    int l = System.Math.Min(this.Length, 4) + _offset;
    int val = 0;
    for(int i = _offset; i < l; i++) {
      val = (val << 8) | _buffer[i];
    }
    _hc = val;
    _have_hc = true;
    return val;
  }
  /**
   * Use the given Encoding to read a string out of the MemBlock
   */
  public string GetString(System.Text.Encoding e)
  {
    if( _length != 0 ) {
      return e.GetString(_buffer, _offset, _length);
    }
    else {
      return System.String.Empty;
    }
  }

  /**
   * Write the MemBlock to a stream.
   * If your insane Stream modifies the buffer as it is
   * writing it, be prepared for hard to find bugs.
   */
  public void WriteTo(System.IO.Stream s) {
    if( _length > 0 ) {
      s.Write(_buffer, _offset, _length);
    }
  }
  /**
   * Make a reference to the given byte array, it does not make a copy.
   * This is used rather than a constructor to make it obvious to the
   * caller that this is only making a reference, not a copy
   */
  static public MemBlock Reference(byte[] data, int offset, int length) {
    if( length != 0 ) { 
      return new MemBlock(data, offset, length);
    }
    else {
      return _null;
    }
  }
  /**
   * Same as the above with offset = 0 and length the whole array
   */
  static public MemBlock Reference(byte[] data) {
    return Reference(data, 0, data.Length);
  }
  
  /**
   * Search through the current buffer for a byte b, and return
   * the index to it.  If it is not found, return -1
   */
  public int IndexOf(byte b)
  {
    int max = _offset + _length;
    for(int idx = _offset; idx < max; idx++) {
      if( _buffer[idx] == b ) {
        return (idx - _offset);
      }
    }
    return -1;
  }

  /**
   * Returns a new MemBlock which starts at a given offset in the
   * current block and runs a given total length
   */
  public MemBlock Slice(int offset, int length) {
    return new MemBlock(_buffer, _offset + offset, length);
  }
  
  /**
   * Return the "tail" of the current MemBlock starting at a given
   * offset.
   */
  public MemBlock Slice(int offset) {
    return new MemBlock(_buffer, _offset + offset, _length - offset);
  }

  /**
   * Returns a read-only MemoryStream of the current MemBlock
   */
  public System.IO.MemoryStream ToMemoryStream() {
    return new System.IO.MemoryStream(_buffer, _offset, _length, false);
  }

  /**
   * convert the MemBlock to base64
   */
  public string ToBase64String() {
    return System.Convert.ToBase64String(_buffer,_offset,_length);
  }

  /**
   * convert the MemBlock to base32 with padding
   */
  public string ToBase32String() {
    return Base32.Encode(_buffer, _offset, _length, true);
  }

  public string ToBase16String() {
    System.Text.StringBuilder sb = new System.Text.StringBuilder(_length * 2);
    int max = _offset + _length;
    for(int i = _offset; i < max; i++)
    {
      sb.AppendFormat("{0:x2}", _buffer[i]);
    }
    return sb.ToString();
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
      Assert.AreEqual(mb1, data, "MemBlock == byte[]");
      MemBlock mb2 = new MemBlock(data, offset, data.Length - offset);
      MemBlock mb2a = mb1.Slice(offset);
      MemBlock mb3 = new MemBlock(data, 0, offset);
      MemBlock mb3a = mb1.Slice(0, offset);
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
  [Test]
  public void SomeInsanityTests() {
    byte[] data;
    bool got_x;
    MemBlock b;
    System.Random r = new System.Random();
    for(int i = 0; i < 100; i++) {
     int size = r.Next(1024);
     data = new byte[size];
     r.NextBytes(data);
     int overshoot = r.Next(1,1024);
     got_x = false;
     b = null;
     try {
      //Should throw an exception:
      b = MemBlock.Reference(data, 0, size + overshoot);
     }
     catch {
      got_x = true;
     }
     Assert.IsNull(b, "Reference failure test");
     Assert.IsTrue(got_x, "Exception catch test");
     
     overshoot = r.Next(1,1024);
     got_x = false;
     b = MemBlock.Reference(data);
     try {
      //Should throw an exception:
      byte tmp = b[size + overshoot];
     }
     catch {
      got_x = true;
     }
     Assert.IsTrue(got_x, "index out of range exception");
     got_x = false;
     try {
      //Should throw an exception:
      byte tmp = b[ b.Length ];
     }
     catch {
      got_x = true;
     }
     Assert.IsTrue(got_x, "index out of range exception");
   }
  }

#endif
}

}
