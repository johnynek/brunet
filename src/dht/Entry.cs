using System;
using System.IO;

namespace Brunet.Dht {

  public class Entry {
    protected MemBlock _key;
    public MemBlock Key { get { return _key; } }

    protected MemBlock _data;
    public virtual MemBlock Data { get { return _data; } }

    protected DateTime _create_time;
    public DateTime CreatedTime { get { return _create_time; } }

    protected DateTime _end_time;
    public DateTime EndTime { get { return _end_time; } }

    protected int _index;
    public int Index { get { return _index; } }

    public virtual void Delete() {;}

    /**
    * This holds a unique (increasing) index for this entry
    */
    protected int _idx;

    public Entry(MemBlock key, DateTime create_time, DateTime end_time, 
                 MemBlock data, int idx) {
      _key = key;
      _index = idx;
      _data = data;
      _create_time = create_time;
      _end_time = end_time;
    }
  }
  public class DiskEntry: Entry {
    protected static readonly int _MAX_DATA = 1000;

    protected string _file;

    public override MemBlock Data {
      get {
        MemBlock data = null;
        using (BinaryReader br = new BinaryReader(File.Open(_file, FileMode.Open))) {
          data = br.ReadBytes(_MAX_DATA);
          br.Close();
        }
        return data;
      }
    }

    public string GenerateDirectory(string base_path, MemBlock key, int index) {
      string[] l = new string[5];
      for (int j = 0; j < 4; j++) {
        l[j] = string.Empty;
      }

      l[0] = base_path;
      l[1] = key[0].ToString();
      l[2] = key[1].ToString();
      l[3] = key[2].ToString();

      for (int i = 3; i < 20; i++) {
        l[4] += key[i].ToString();
      }

      string path = String.Join(Path.DirectorySeparatorChar.ToString(), l);
      if(!Directory.Exists(path)) {
        Directory.CreateDirectory(path);
      }
      return Path.Combine(path, index.ToString());
    }

    public DiskEntry(string base_dir, MemBlock key, DateTime create_time,
                      DateTime end_time, MemBlock data, int idx) :
                    base(key, create_time, end_time, null, idx) {
      _file = GenerateDirectory(base_dir, key, idx);
      using (BinaryWriter bw = new BinaryWriter(File.Open(_file, FileMode.Create))) {
        bw.Write((byte[]) data);
        bw.Flush();
        bw.Close();
      }
    }

    public override void Delete() {
      if(File.Exists(_file)) {
        File.Delete(_file);
      }
    }
  }
}
