/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>  University of Florida

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

namespace Brunet {

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
   * how many bytes would this take to represent
   */
  int Length { get; }
}

/**
 * Join (without yet copying) a set of ICopyable objects
 */
public class CopyList : ICopyable {
  
  protected ICopyable[] _cs;
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
