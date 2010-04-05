/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>  University of Florida

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

namespace Brunet.Util {

/**
 * A simple byte serialization interface
 */
public interface ICopyable {

  /**
   * @param dest the byte array to copy to
   * @param offset the position to start at
   * @return the number of bytes written
   */
  int CopyTo(byte[] dest, int offset); 
  /**
   * @return How many bytes would this take to represent.
   *
   * Prefer not to call this method.  It may require as much work
   * as CopyTo internally, so if you can write first and then
   * get the length written returned from CopyTo, it will be faster
   * to do so.
   */
  int Length { get; }
}

/**
 * Join (without yet copying) a set of ICopyable objects
 */
public class CopyList : ICopyable {
  
  protected ICopyable[] _cs;
  
  /**
   * How many Copyable objects are in this list
   */
  public int Count {
    get { return _cs.Length; }
  }
  
  /**
   * Get out individual elements from the list
   */
  public ICopyable this[int idx] {
    get { return _cs[idx]; }
  }

  /**
   * @param cs is an IEnumerable of ICopyable objects
   */
  public CopyList(params ICopyable[] cs) {
    _cs = cs;
  }
  
  /**
   * Copy in order the entire set
   */
  public int CopyTo(byte[] dest, int offset) {
    int total = 0;
    for(int i = 0; i < _cs.Length; i++) {
      ICopyable c = _cs[i];
      total += c.CopyTo(dest, offset + total);
    }
    return total;
  }
  public int Length {
    get {
      int total_length = 0;
      for(int i = 0; i < _cs.Length; i++) {
        ICopyable c = _cs[i];
        total_length += c.Length;
      }
      return total_length;
    }
  }
}

}
