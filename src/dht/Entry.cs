using System;
using System.IO;
using System.Collections;

namespace Brunet.Dht {

  public class Entry {
    Hashtable stored_data = null;
    public virtual byte[] Key { get { return (byte[]) stored_data["_key"]; } }
    public virtual byte[] Data { get { return (byte[]) stored_data["_data"]; } }
    public virtual DateTime CreatedTime { get { return (DateTime) stored_data["_create_time"]; } }
    public virtual DateTime EndTime { get { return (DateTime) stored_data["_end_time"]; } }
    public virtual string Password { get { return (string) stored_data["_password"]; } }
    public virtual int Index { get { return (int) stored_data["_index"]; } }

    public Entry() {;}

    public Entry(byte[] key, string password, DateTime create_time,
                  DateTime end_time, byte[] data, int idx) {
      stored_data = new Hashtable();
      stored_data["_key"] = key;
      stored_data["_password"] = password;
      stored_data["_index"] = idx;
      stored_data["_data"] = data;
      stored_data["_create_time"] = create_time;
      stored_data["_end_time"] = end_time;
    }

    public virtual void Delete() {;}
  }

  public class DiskEntry: Entry {
    private string _file;

    public override byte[] Key {
      get {
        DhtData dhtdata = DhtDataHandler.Read(_file);
        return dhtdata.key;
      }
    }

    public override byte[] Data {
      get {
        DhtData dhtdata = DhtDataHandler.Read(_file);
        return dhtdata.value;
      }
    }

    public override DateTime CreatedTime {
      get {
        DhtData dhtdata = DhtDataHandler.Read(_file);
        return dhtdata.created_time;
      }
    }

    public override DateTime EndTime {
      get {
        DhtData dhtdata = DhtDataHandler.Read(_file);
        return dhtdata.end_time;
      }
    }

    public override string Password {
      get {
        DhtData dhtdata = DhtDataHandler.Read(_file);
        return dhtdata.password;
      }
    }

    public override int Index {
      get {
        DhtData dhtdata = DhtDataHandler.Read(_file);
        return dhtdata.index;
      }
    }

    public string GenerateDirectory(byte[] key) {
      string[] l = new string[4];
      for (int j = 0; j < 4; j++) {
        l[j] = string.Empty;
      }

      for (int i = 0; i < 5; i++) {
        for (int j = 0; j < 4; j++) {
          l[j] += key[i].ToString();
        }
      }

      return String.Join(Path.DirectorySeparatorChar.ToString(), l);
    }

    public DiskEntry(string base_directory, byte[] key, string password, DateTime create_time, 
                      DateTime end_time, byte[] data, int idx) {
      _file = Path.Combine(base_directory, GenerateDirectory(key));
      Directory.CreateDirectory(_file);
      _file += Path.DirectorySeparatorChar.ToString() + idx;
      DhtData dhtdata = new DhtData();
      dhtdata.key = key;
      dhtdata.value = data;
      dhtdata.password = password;
      dhtdata.created_time = create_time;
      dhtdata.end_time = end_time;
      dhtdata.index = idx;
      DhtDataHandler.Write(_file, dhtdata);
    }

    /* I don't trust the destructor ... */
    public override void Delete() {
      if(File.Exists(_file)) {
        File.Delete(_file);
      }
    }
  }
}
