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
using System.Collections;

namespace Brunet.DistributedServices {
  /**
  <summary>An entry contains the data for a key:value pair such as the key,
  value, create time, and end time.  The data is stored in a hashtable, which
  allows this to be casted to and from a hash table.</summary>
  */
  public class Entry {
    /// <summary>The hashtable where Entry data is stored.</summary>
    protected Hashtable _ht = new Hashtable(4);

    /**
    <summary>Provides the ability to cast from an Entry to a hashtable.
    </summary>
    <returns>The data store hashtable</returns>
    */
    public static explicit operator Hashtable(Entry e) {
      return e._ht;
    }

    /**
    <summary>Provides conversion from a hashtable to an Entry object</summary>
    <returns>A new Entry object using the hashtable as the data store</returns>
    */
    public static explicit operator Entry(Hashtable ht) {
      return new Entry(ht);
    }

    /// <summary>The dht key used for indexing this data.</summary>
    public MemBlock Key {
      get { return (MemBlock) _ht["key"]; }
      set { _ht["key"] = value; }
    }

    /**  <summary>A single value stored at this key.</summary>
    <remarks>EndTime is stored as a ulong for memory and serialization
    purposes.</remarks>
    */
    public MemBlock Value {
      get { return (MemBlock) _ht["value"]; }
      set { _ht["value"] = value; }
    }

    /**  <summary>The create time for this key:value pair.</summary>
    <remarks>CreateTime is stored as a ulong for memory and serialization
    purposes.</remarks>
    */
    public DateTime CreateTime {
      get { return DateTime.MinValue.AddSeconds((ulong) _ht["create_time"]); }
      set { _ht["create_time"] = Convert.ToUInt64((value - DateTime.MinValue).TotalSeconds);  }
    }

    /// <summary>The time the lease expires for this key:value pair.</summary>
    public DateTime EndTime {
      get { return DateTime.MinValue.AddSeconds((ulong) _ht["end_time"]); }
      set { _ht["end_time"] = Convert.ToUInt64((value - DateTime.MinValue).TotalSeconds);  }
    }

    /**
    <summary>Creates a new Entry given the key, data, create time, and end time
    </summary>
    <param name="key">The dht key used for indexing this data.</param>
    <param name="data">A single value stored at this key.</param>
    <param name="create_time">The initial creation time for this key:value
    pair.</param>
    <param name="end_time">The time the lease expires for this key:value pair.
    </param>
    */
    public Entry(MemBlock key, MemBlock data, DateTime create_time,
      DateTime end_time) {
      this.Key = key;
      this.Value = data;
      this.CreateTime = create_time;
      this.EndTime = end_time;
    }

    /**
    <summary>Uses the hashtable as the data store for the dht</summary>
    <param name="ht">A hashtable containing key, value, create_time, and
    end_time strings as keys</param>
    */
    public Entry(Hashtable ht) {
      _ht = ht;
    }

    /**
    <summary>Compares the hashcodes for two Entrys.</summary>
    <returns>True if they are equal, false otherwise.</returns>
    */
    public override bool Equals(Object ent) {
      return (this.GetHashCode() == ent.GetHashCode());
    }

    /**
    <summary>Gets the hashcode for an Entry object computed by the
    Key.GetHashCode() xor Value.GetHashCode().</summary>
    <returns>The hashcode.</returns>
    */
    public override int GetHashCode() {
      return (Key.GetHashCode() ^ Value.GetHashCode());
    }
  }
}
