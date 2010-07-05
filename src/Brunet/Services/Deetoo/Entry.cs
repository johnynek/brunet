/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2008 Taewoong Choi <twchoi@ufl.edu> University of Florida  

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
using System.Collections;

using Brunet.Util;
using Brunet.Symphony;
namespace Brunet.Services.Deetoo
{
  /**
   <summary>An entry contains the data for a key:value pair
   such as content, replication factor(alpha), start_address, end_address.
   The data is stored in a hashtable, which allows this to be casted to and from a hashtable.</summary>
   */
  public class Entry {
    /* <summary>The actual content(for now, it is a string).</summary>
    <remarks>Content is stored as a string for now. </remarks>
    */
    public readonly object Content;
    /// <summary>Replication factor for deciding bounded broadcasting range. </summary>
    public readonly double Alpha;
    /// <summary> Start address of a range. </summary>
    public readonly AHAddress Start;
    /// <summary> End address of a range. </summary>
    public readonly AHAddress End;
    /**
    <summary>Creates a new Entry given the content, alpha, start address, and end address.</summary>
    <param name="content">The content which is replicated in a range.</param>
    <param name="alpha">A replication factor which decides bounded broadcasting range.</param>
    <param name="start">The start address of bounded broadcasting range.</param>
    <param name="end">The end address of bounded broadcasting range.</param>
    </param>
    */    
    public Entry(object content, double alpha, AHAddress start, AHAddress end) {
      Content = content;
      Alpha = alpha;
      Start = start;
      End = end;
    }
    /**
    <summary>Reassign range info(Start and End) based on recalculated range.</summary>
    <param name = "rg_size">Current range size(round distance between start address and end address of this Entry).</param>
    <remarks>new_start = mid - rg_size/2, new_end = mid + rg_size/2 </remarks>
     */
    public Entry ReAssignRange(BigInteger rg_size) {
      // calculate middle address of range
      BigInteger start_int = Start.ToBigInteger();
      BigInteger end_int = End.ToBigInteger();
      BigInteger mid_int =  (start_int + end_int) / 2;  
      if (mid_int % 2 == 1) { mid_int = mid_int -1; }
      AHAddress mid_addr = new AHAddress(mid_int);
      /*
       * If we have a case where start -> end includes zero,
       * this is the wrap around.  So, we can imagine that
       * we have end' = end + Address.Full.  So,
       * mid' = (start + end')/2 = (start + end)/2 + Address.Full/2
              = (start + end)/ 2 + Address.Half
       */
      if (!mid_addr.IsBetweenFromLeft(Start, End)) {
        mid_int += Address.Half;
      }
      //addresses for new range
      BigInteger rg_half = rg_size / 2;
      if (rg_half % 2 == 1) { rg_half -= 1; }
      AHAddress n_a = new AHAddress(mid_int - rg_half);
      AHAddress n_b = new AHAddress(mid_int + rg_half);
      return new Entry(Content, Alpha, n_a, n_b);
    }
  }
}
