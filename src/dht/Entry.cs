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

namespace Brunet.Dht {

  public class Entry {
    private Hashtable _ht = new Hashtable(5);
    public static explicit operator Hashtable(Entry e) {
      return e._ht;
    }

    public static explicit operator Entry(Hashtable ht) {
      return new Entry(ht);
    }


    public MemBlock Key {
      get { return (MemBlock) _ht["key"]; }
      set { _ht["key"] = value; }
    }

    public MemBlock Value {
      get { return (MemBlock) _ht["value"]; }
      set { _ht["value"] = value; }
    }

    public DateTime CreateTime {
      get { return DateTime.MinValue.AddSeconds((ulong) _ht["create_time"]); }
      set { _ht["create_time"] = Convert.ToUInt64((value - DateTime.MinValue).TotalSeconds);  }
    }

    public DateTime EndTime {
      get { return DateTime.MinValue.AddSeconds((ulong) _ht["end_time"]); }
      set { _ht["end_time"] = Convert.ToUInt64((value - DateTime.MinValue).TotalSeconds);  }
    }

    public Entry(MemBlock key, MemBlock data, DateTime create_time, DateTime end_time) {
      this.Key = key;
      this.Value = data;
      this.CreateTime = create_time;
      this.EndTime = end_time;
    }

    public Entry(Hashtable ht) {
      _ht = ht;
    }

    public override bool Equals(Object ent) {
      return (this.GetHashCode() == ent.GetHashCode());
    }

    public override int GetHashCode() {
      return (Key.GetHashCode() ^ Value.GetHashCode());
    }
  }
}
