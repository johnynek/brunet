using System;

namespace Brunet.Dht {

public class Entry {
 
  protected byte[] _key;
  public byte[] Key { get { return _key; } }

  protected byte[] _data;
  public byte[] Data { get { return _data; } }

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
  
  public Entry(byte[] key, string password,
               DateTime create_time, DateTime end_time,
	       byte[] data, int idx)
  {
    _key = key;
    _password = password;
    _data = data;
    _index = idx;
    _create_time = create_time;
    _end_time = end_time;
  }

}
	
}
