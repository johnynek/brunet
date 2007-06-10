using System;
using System.Text;
using System.Collections;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Brunet.Dht{
  public class DhtData {
    public byte[] key, value;
    public string password;
    public int index, ttl;
    public DateTime created_time, end_time;
  }

  public class DhtDataHandler {
    public static DhtData Read(string configFile) {
      XmlSerializer serializer = new XmlSerializer(typeof(DhtData));
      DhtData dhtdata;
      using(FileStream fs = new FileStream(configFile, FileMode.Open)) {
        dhtdata = (DhtData) serializer.Deserialize(fs);
        fs.Close();
      }
      return dhtdata;
    }

    public static void Write(string configFile, DhtData config) {
      using(FileStream fs = new FileStream(configFile, FileMode.Create, FileAccess.Write)) {
        XmlSerializer serializer = new XmlSerializer(typeof(DhtData));
        serializer.Serialize(fs, config);
        fs.Close();
      }
    }

/* Tests the above classes...
      public static void Main() {
      DhtData dhtdata = new DhtData();
      dhtdata.key = new byte[]{0, 1, 2, 3, 4, 5, 6};
      dhtdata.value = new byte[]{6, 5, 4, 3, 2, 1, 0};
      dhtdata.password = "Password";
      dhtdata.index = 5;
      dhtdata.created_time = DateTime.UtcNow;
      dhtdata.end_time = DateTime.Now;
      Write("test.txt", dhtdata);
      DhtData readdata = Read("test.txt");
      Console.WriteLine("{0} = {1}", readdata.key.GetHashCode().ToString(), readdata.value.GetHashCode().ToString());
      Console.WriteLine("Start time: {0}, End time: {1}", readdata.created_time.ToString(), readdata.end_time.ToString());
      Console.WriteLine("Password = " + readdata.password);
      Console.WriteLine("Index = " + readdata.index);
    }
*/
  }
}
