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
}

namespace Brunet.Dht {
    /// <summary>
    /// An entry of the array returned by Get(...) in Dht interfaces.
    /// </summary>
  [Serializable]
  public class DhtGetResult {
    private static readonly string HT_KEY_VALUE = "value";
    private static readonly string HT_KEY_AGE = "age";

    public int age;
    public byte[] value;

    /// <summary>
    /// A String representation of the field byte[] data for convenience
    /// </summary>
    public string valueString { 
      get { return Encoding.UTF8.GetString(value); }
      // XmlRpc complains if this doesn't exist
      set { ; }
    }

    public DhtGetResult() {;}

    public DhtGetResult(string value, int age)
    {
      this.value = Encoding.UTF8.GetBytes(value);
      this.age = age;
    }

    public DhtGetResult(Hashtable ht) {
      this.age = (int) ht[HT_KEY_AGE];
      this.value = (byte[]) ht[HT_KEY_VALUE];
    }

    /// <summary>
    /// All Explicit conversion
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    /// <example>Hashtable ht = (Hashtable)item</example>
    public static explicit operator Hashtable(DhtGetResult item) {
      Hashtable ht = new Hashtable();
      ht.Add(HT_KEY_VALUE, item.value);
      ht.Add(HT_KEY_AGE, item.age);
      ht.Add("value_string",item.valueString);
      return ht;
    }

    public override string ToString() {
      return string.Format("{0}={1}, {2}={3}", HT_KEY_VALUE, valueString,  HT_KEY_AGE, age);
    }
  }
}

