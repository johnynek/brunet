using System;
using System.Text;
using System.Collections;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

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
