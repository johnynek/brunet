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

namespace Brunet.Dht {
  [Serializable]
  public class DhtGetResult {
    public int age, ttl;
    public byte[] value;

    public static DhtGetResult Empty() {
      return new DhtGetResult(new byte[0], 0, 0);
    }

    public string valueString {
      get { return Encoding.UTF8.GetString(value); }
      // XmlRpc complains if this doesn't exist
      set { ; }
    }

    public DhtGetResult() {;}

    public DhtGetResult(string value, int age) {
      this.value = MemBlock.Reference(Encoding.UTF8.GetBytes(value));
      this.age = age;
    }

    public DhtGetResult(byte[] value, int age, int ttl) {
      this.value = value;
      this.age = age;
      this.ttl = ttl;
    }

    public DhtGetResult(Hashtable ht) {
      this.value = (byte[]) ht["value"];
      this.age = (int) ht["age"];
      this.ttl = (int) ht["ttl"];
    }

    public static explicit operator Hashtable(DhtGetResult dgr) {
      Hashtable ht = new Hashtable();
      ht.Add("age", dgr.age);
      ht.Add("value", dgr.value);
      ht.Add("ttl", dgr.ttl);
      return ht;
    }

    public override string ToString() {
      return string.Format("value = {0}, age = {1}", valueString, age);
    }

    public bool IsEmpty() {
      return (value.Length == 0 && age == 0 && ttl == 0);
    }
  }
}