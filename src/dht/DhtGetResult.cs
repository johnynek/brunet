using System;
using System.Text;
using System.Collections;

namespace Brunet.Dht {
  [Serializable]
  public class DhtGetResult {
    public int age;
    public byte[] value;

    public string valueString {
      get { return Encoding.UTF8.GetString(value); }
      // XmlRpc complains if this doesn't exist
      set { ; }
    }

    public DhtGetResult() {;}

    public DhtGetResult(string value, int age) {
      this.value = Encoding.UTF8.GetBytes(value);
      this.age = age;
    }

    public DhtGetResult(byte[] value, int age) {
      this.value = value;
      this.age = age;
    }

    public DhtGetResult(Hashtable ht) {
      this.value = (byte[]) ht["value"];
      this.age = (int) ht["age"];
    }

    public static explicit operator Hashtable(DhtGetResult dgr) {
      Hashtable ht = new Hashtable();
      ht.Add("age", dgr.age);
      ht.Add("value", dgr.value);
      return ht;
    }

    public override string ToString() {
      return string.Format("value = {0}, age = {1}", valueString, age);
    }
  }
}