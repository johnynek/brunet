using System;
using System.IO;

namespace Brunet.Dht {

  public class Entry {
    protected byte[] _key;
    public byte[] Key { get { return _key; } }

    protected byte[] _data;
    public virtual byte[] Data { get { return _data; } }

    protected DateTime _create_time;
    public DateTime CreatedTime { get { return _create_time; } }

    protected DateTime _end_time;
    public DateTime EndTime { get { return _end_time; } }

    protected string _password;
    public string Password { get { return _password; } }

    protected int _index;
    public int Index { get { return _index; } }

    /**
    * This holds a unique (increasing) index for this entry
    */
    protected int _idx;

    public Entry(byte[] key, string password, DateTime create_time,
                  DateTime end_time, byte[] data, int idx) {
      _key = key;
      _password = password;
      _index = idx;
      _data = data;
      _create_time = create_time;
      _end_time = end_time;
    }
  }
  public class DiskEntry: Entry {
    protected static readonly int _MAX_DATA = 1000;

    protected string _file;

    public override byte[] Data {
      get {
        FileStream fStream = new FileStream(_file, FileMode.Open, FileAccess.Read);
        BinaryReader br = new BinaryReader(fStream);
        byte[] data = br.ReadBytes(_MAX_DATA);
        br.Close();
        fStream.Close();
        return data;
      }
    }

    public DiskEntry(string fname, byte[] key, string password, DateTime create_time, 
                      DateTime end_time, byte[] data, int idx) :
                    base(key, password, create_time, end_time, null, idx) {
      object o = (object) idx;
      _file = Path.Combine(fname, o.ToString());
      FileStream fStream = new FileStream(_file, FileMode.Create);
      BinaryWriter bw = new BinaryWriter(fStream);
      bw.Write(data);
      bw.Flush();
      bw.Close();
      fStream.Close();
    }
  }
}
