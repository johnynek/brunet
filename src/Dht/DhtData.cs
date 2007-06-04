using System;
using System.Text;
using System.Collections;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

/* These need to be merged */

namespace Ipop {
  public class DhtData {
    public string key, value, ttl, password;
  }

  public class DhtDataHandler {
    public static DhtData Read(string configFile) {
      FileStream fs = new FileStream(configFile, FileMode.Open, FileAccess.Read);
      StreamReader sr = new StreamReader(fs);
      DhtData data = new DhtData();
      while(sr.Peek() >= 0) {
        string line = sr.ReadLine();
        int equal_pos = line.IndexOf("=");
        string type = line.Substring(0, equal_pos);
        string value = line.Substring(equal_pos + 1, line.Length - equal_pos - 1);
        switch(type) {
          case "key":
            data.key = value;
            break;
          case "value":
            data.value = value;
            break;
          case "ttl":
            data.ttl = value;
            break;
          case "password":
            data.password = value;
            break;
        }
      }
      sr.Close();
      fs.Close();
      return data;
    }

    public static void Write(string configFile, DhtData data) {
      FileStream fs = new FileStream(configFile, FileMode.Create, 
        FileAccess.Write);
      StreamWriter sw = new StreamWriter(fs);
      sw.WriteLine("key=" + data.key);
      sw.WriteLine("value=" + data.value);
      sw.WriteLine("ttl=" + data.ttl);
      sw.WriteLine("password=" + data.password);
      sw.Close();
      fs.Close();
    }
  }

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

    public override bool Equals(Object odgr) {
      DhtGetResult dgr = (DhtGetResult) odgr;
      if(dgr.value.Length != this.value.Length) {
        return false;
      }
      for(int i = 0; i < this.value.Length; i++) {
        if(dgr.value[i] != this.value[i]) {
          return false;
        }
      }
      return true;
    }

    public override int GetHashCode() {
      return value.GetHashCode();
    }

/*    public static bool operator ==(DhtGetResult dgr0, DhtGetResult dgr1) {
      if(dgr0.value == dgr1.value)
        return true;
      return false;
    }

    public static bool operator !=(DhtGetResult dgr0, DhtGetResult dgr1) {
      if(dgr0.value == dgr1.value)
        return true;
      return false;
    }*/
  }
}
