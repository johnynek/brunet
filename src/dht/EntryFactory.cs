using System;
using System.Collections;
using System.IO;

namespace Brunet.Dht {
  public class EntryFactory {
    /** static hashtable to keep track of EntryFactory objects. */
    protected static Hashtable _ef_table = null;

    private Node _node;
    public enum Media {
      Invalid,
      Memory,
      Disk,
    };

    private Media _media;

    public void SetMedia(Media media) {
      _media = media;
      if (_media == Media.Disk) {
        string dir_path = Path.Combine("data", _node.Address.ToString().Substring(12));
        if(Directory.Exists(dir_path)) {
            Directory.Delete(dir_path, true);
        }
        Directory.CreateDirectory(dir_path);
      }
    }

    private EntryFactory(Node node) {
      _node = node;
    }

    public static EntryFactory GetInstance(Node node) {
      // Returns an ef for a node or creates one if non-exists
      if (_ef_table == null) {
        _ef_table = new Hashtable();
      }
      if (_ef_table.ContainsKey(node)) {
        return (EntryFactory) _ef_table[node];
      }
      EntryFactory ef = new EntryFactory(node);
      _ef_table[node] = ef;
      return ef;
    }

    public Entry CreateEntry(byte[] key, string password, DateTime create_time,
                                    DateTime end_time, byte[] data, int idx) {
      if (_media == Media.Invalid) {
        return null;
      }
      if(_media == Media.Memory) {
        return new Entry(key, password, create_time, end_time, data, idx);
      }
      if (_media == Media.Disk) {
        return new DiskEntry(Path.Combine("data",
          _node.Address.ToString().Substring(12)), key, password, create_time,
          end_time, data, idx);
      }
      return null;
    }
  }
}
