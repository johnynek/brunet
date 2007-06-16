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
    private string _dir_path;

    public void CleanUp(Object o, EventArgs args) {
      this.CleanUp();
    }

    private void CleanUp() {
      if (_media == Media.Disk) {
        if(Directory.Exists(_dir_path)) {
            Directory.Delete(_dir_path, true);
        }
      }
    }

    private void SetMedia(Media media) {
      _media = media;
      if (_media == Media.Disk) {
        _dir_path = Path.Combine("data", _node.Address.ToString().Substring(12));
        CleanUp();
        Directory.CreateDirectory(_dir_path);
        _node.DepartureEvent += this.CleanUp;
      }
    }

    private EntryFactory(Node node, Media m) {
      _node = node;
      SetMedia(m);
    }

    public static EntryFactory GetInstance(Node node, Media media) {
      // Returns an ef for a node or creates one if non-exists
      if (_ef_table == null) {
        _ef_table = new Hashtable();
      }
      EntryFactory ef = (EntryFactory) _ef_table[node];
      if ( ef == null) {
        ef = new EntryFactory(node, media);
        _ef_table[node] = ef;
      }
      if( ef._media != media ) {
        //someone called GetInstance again with a different media:
	throw new Exception(
	   String.Format("Tried to make another EntryFactory for node {0} with different media {1} != {2}",
	                 node, ef._media, media) );
      }
      return ef;
    }

    public Entry CreateEntry(MemBlock key, DateTime create_time, DateTime end_time,
                             MemBlock data, int idx) {
      if (_media == Media.Invalid) {
        return null;
      }
      if(_media == Media.Memory) {
        return new Entry(key, create_time, end_time, data, idx);
      }
      if (_media == Media.Disk) {
        return new DiskEntry(_dir_path, key, create_time, end_time, data, idx);
      }
      return null;
    }
  }
}
