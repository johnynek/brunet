/*
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Text;
using System.Collections;

namespace Brunet.DistributedServices {
  /**
  <summary>This class provides a mechanism to store the dht get results as a 
  single unified object.  It also provides the ability to convert the retrieved
  value to a utf8 string.</summary>
  */
  [Serializable]
  public class DhtGetResult {
    /**  <summary>The age the server has recorded for this object in seconds.
    </summary>*/
    public int age;
    /// <summary>The dht lease time left for this object in seconds.</summary>
    public int ttl;
    /// <summary>The data stored for this object.</summary>
    public byte[] value;

    /// <summary>Creates an empty DhtGetResult.</summary>
    public static DhtGetResult Empty() {
      return new DhtGetResult(new byte[0], 0, 0);
    }

    /// <summary>Converts the byte array into a UTF8 encoded string.</summary>
    public string valueString {
      get { return Encoding.UTF8.GetString(value); }
      // XmlRpc complains if this doesn't exist
      set { ; }
    }

    /// <summary>The default constructor, do not use!</summary>
    public DhtGetResult() {;}

    /**
    <summary>Creates a new DhtGetResult from a string value.</summary>
    <param name="value">The data stored for this object.</param>
    <param name="age">The age in seconds the server has recorded for this
    object.</param>
    */
    public DhtGetResult(string value, int age) {
      this.value = MemBlock.Reference(Encoding.UTF8.GetBytes(value));
      this.age = age;
    }

    /**
    <summary>Creates a new DhtGetResult from a byte array value.</summary>
    <param name="value">The data stored for this object.</param>
    <param name="age">The age in seconds the server has recorded for this
    object.</param>
    <param name="ttl">The dht lease time in seconds left for this object.
    </param>
    */
    public DhtGetResult(byte[] value, int age, int ttl) {
      this.value = value;
      this.age = age;
      this.ttl = ttl;
    }

    /**
    <summary>Converts a hashtable into a DhtGetResult.</summary>
    <param name="ht">The hashtable containing a value, age, and ttl.</param>
    <exception>Throws exception if the hashtable is missing value, age, or ttl.
    </exception>
    */
    public DhtGetResult(Hashtable ht) {
      this.value = (byte[]) ht["value"];
      this.age = (int) ht["age"];
      this.ttl = (int) ht["ttl"];
    }

    /**
    <summary>Converts the DhtGetResult to a hashtable.</summary>
    <param name="dgr">The DhtGetResult to convert.</param>
    */
    public static explicit operator Hashtable(DhtGetResult dgr) {
      Hashtable ht = new Hashtable();
      ht.Add("age", dgr.age);
      ht.Add("value", dgr.value);
      ht.Add("ttl", dgr.ttl);
      return ht;
    }

    /**
    <summary>Returns the value and age for this result as a string.</summary>
    <returns>The string containing the value and age.</returns>
    */
    public override string ToString() {
      return string.Format("value = {0}, age = {1}", valueString, age);
    }

    /**
    <summary>Determines if the value is empty.  This uses the defintion as
    defined in Empty().</summary>
    <returns>True if empty, false otherwise.</returns>
    */
    public bool IsEmpty() {
      return (value.Length == 0 && age == 0 && ttl == 0);
    }
  }
}