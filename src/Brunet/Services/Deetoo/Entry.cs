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
    /// <summary>The hashtable where Entry data is stored.</summary>
    protected Hashtable _ht = new Hashtable(4);
    /**
    <summary>Provides the ability to cast from an Entry to a hashtable.
    </summary>
    <returns>The data store hashtable</returns>
    */    
    public static explicit operator Hashtable(Entry c) {
      return c._ht;
    }
    
    /**
    <summary>Provides conversion from a hashtable to an Entry object</summary>
    <returns>A new Entry object using the hashtable as the data store</returns>
    */
    public static explicit operator Entry(Hashtable ht) {
      return new Entry(ht);
    }
    /* <summary>The actual content(for now, it is a string).</summary>
    <remarks>Content is stored as a string for now. </remarks>
    */
    public string Content {
      get { return (string) _ht["content"]; }
      set { _ht["content"] = value; }
    }
    /// <summary>Replication factor for deciding bounded broadcasting range. </summary>
    public double Alpha {
      get { return (double) _ht["alpha"]; }
      set { _ht["alpha"] = value; }
    }
    /// <summary> Start address of a range. </summary>
    public Address Start {
      get { return (Address) _ht["start"]; }
      set { _ht["start"] = value; }
    }
    /// <summary> End address of a range. </summary>
    public Address End {
      get { return (Address) _ht["end"]; }
      set { _ht["end"] = value; }
    }
    /**
    <summary>Creates a new Entry given the content, alpha, start address, and end address.</summary>
    <param name="content">The content which is replicated in a range.</param>
    <param name="alpha">A replication factor which decides bounded broadcasting range.</param>
    <param name="start">The start address of bounded broadcasting range.</param>
    <param name="end">The end address of bounded broadcasting range.</param>
    </param>
    */    
    public Entry(string content, double alpha, Address start, Address end) {
      this.Content = content;
      this.Alpha = alpha;
      this.Start = start;
      this.End = end;
    }
    /**
    <summary>Uses the hashtable as the data store for the Deetoo data</summary>
    <param name="ht">A hashtable containing content,alpha, start, and end as keys</param>
    */
    public Entry(Hashtable ht) {
      _ht = ht;
    }    
    /**
    <summary>Compares the contents for two Entrys.</summary>
    <returns>True if they are equal, false otherwise.</returns>
    */
    public bool Equal(Entry ce) {
      if (this.Content == ce.Content) {
        return true;
      }
      else
      {
        return false;
      }
    }
    /**
    <summary>Reassign range info(Start and End) based on recalculated range.</summary>
    <param name = "rg_size">Current range size(round distance between start address and end address of this Entry).</param>
    <remarks>new_start = mid - rg_size/2, new_end = mid + rg_size/2 </remarks>
     */
    public void ReAssignRange(BigInteger rg_size) {
      AHAddress start_addr = (AHAddress)this.Start;
      AHAddress end_addr = (AHAddress)this.End;
      // calculate middle address of range
      BigInteger start_int = start_addr.ToBigInteger();
      BigInteger end_int = end_addr.ToBigInteger();
      BigInteger mid_int =  (start_int + end_int) / 2;  
      if (mid_int % 2 == 1) { mid_int = mid_int -1; }
      AHAddress mid_addr = new AHAddress(mid_int);
      if (!mid_addr.IsBetweenFromLeft(start_addr, end_addr)) {
        mid_int += Address.Half;
        mid_addr = new AHAddress(mid_int);
      }
      //addresses for new range
      BigInteger rg_half = rg_size / 2;
      if (rg_half % 2 == 1) { rg_half -= 1; }
      BigInteger n_st = mid_int - rg_half;
      /*
      if (n_st < 0) { //underflow
        n_st += AHAddress.Full; 
      }
      */
      BigInteger n_ed = n_st + rg_size;
      /*
      if (n_ed > AHAddress.Full) { //overflow
        n_ed -= AHAddress.Full; 
      }
      */
      /// underflow and overflow are handled by AHAddress class.
      AHAddress n_a = new AHAddress(n_st);
      AHAddress n_b = new AHAddress(n_ed);
      this.Start = n_a;
      this.End = n_b;
    }
    /*
     * determine size of bounded broadcasting range
     */
        /**
    <summary>Determine size of bounded broadcasting range based on estimated network size.</summary>
    <returns>The range size as b biginteger.</returns>
    public BigInteger GetRangeSize(int size) {
      double alpha = this.Alpha;
      double a_n = alpha / (double)size;
      double sqrt_an = Math.Sqrt(a_n);
      double log_san = Math.Log(sqrt_an,2);
      //int exponent = (int)(log_san + 160);
      double exponent = log_san + 160;
      int exponent_i = (int)(exponent) - 63;
      double exponent_f = exponent - exponent_i;
      long twof = (long)Math.Pow(2,exponent_f);
      BigInteger bi_one = new BigInteger(1);
      BigInteger result = (bi_one << exponent_i)*twof;  
      if (result % 2 == 1) { result += 1; } // make this even number.
      if(CacheList.DeetooLog.Enabled) {
        ProtocolLog.Write(CacheList.DeetooLog, String.Format(
          "network size estimation: {0}, new range size: {1}", size, result));
      }
      return result;
    }
    */
  }
}
