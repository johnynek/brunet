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
	//create a directory for the node's data
	Directory.CreateDirectory("data" + OS.OSDir() + _node.Address.ToString());
#if DHT_DEBUG	
	Console.WriteLine("[EntryFactory]: Created a directory: data/{0}", _node.Address);
#endif

      }
    }
    private EntryFactory(Node node) {
      _node = node;
    }

    public static EntryFactory GetInstance(Node node) {
      if (_ef_table == null) {
	_ef_table = new Hashtable();
      }
      //check if there is already an instance object for this node
      if (_ef_table.ContainsKey(node)) {
	return (EntryFactory) _ef_table[node];
      }
      //in case no instance exists, create one
      EntryFactory ef = new EntryFactory(node);
      _ef_table[node] = ef;
      return ef;      
    }
    
    public Entry CreateEntry(byte[] key, string password,
				    DateTime create_time, 
				    DateTime end_time,
				    byte[] data, int idx) {
      if (_media == Media.Invalid) {
	return null;
      }
      if(_media == Media.Memory) {
	return new Entry(key, password, create_time, end_time, data, idx);
      }
      if (_media == Media.Disk) {
	return new DiskEntry("data" + OS.OSDir() + _node.Address.ToString(), key, password, create_time, end_time, data, idx);
      } 

      return null;
    }      
  }
  

}
