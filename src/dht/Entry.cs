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
      get { return DateTime.FromBinary((long) _ht["create_time"]); }
      set { _ht["create_time"] = value.ToBinary(); }
    }

    public DateTime EndTime {
      get {
        return DateTime.FromBinary((long)_ht["end_time"]);
      }
      set {
        _ht["end_time"] = value.ToBinary();
      }
    }

    public Entry(MemBlock key, MemBlock data, DateTime create_time, DateTime end_time) {
      _ht["key"] = key;
      _ht["value"] = data;
      _ht["create_time"] = create_time.ToBinary();
      _ht["end_time"] = end_time.ToBinary();
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
