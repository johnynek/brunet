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
      get { return DateTime.Parse((string) _ht["create_time"]); }
      set { _ht["create_time"] = value.ToString(); }
    }

    public DateTime EndTime {
      get { return DateTime.Parse((string) _ht["end_time"]); }
      set { _ht["end_time"] = value.ToString(); }
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
