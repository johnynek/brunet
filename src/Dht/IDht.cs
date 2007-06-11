using System;
using System.Text;
using Brunet.Dht;

namespace Ipop {
  /// <summary>
  /// A Generic Interface for Dht operations
  /// </summary>
  public interface IDht {
    /// <summary>
    /// Get Dht values
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    DhtGetResult[] Get(string key);

    /// <summary>
    /// Places value in Dht if it is a unique key
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="password"></param>
    /// <param name="ttl"></param>
    /// <returns>Returns a password if it works or NULL if it didn't</returns>
    string Create(string key, string value, string password, int ttl);

    /// <summary>
    /// Places a value in Dht indexed by its key
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="password"></param>
    /// <param name="ttl"></param>
    /// <returns>The password used in this Put operation</returns>
    string Put(string key, string value, string password, int ttl);
  }
}
