using System;
using System.Text;
using Brunet.Dht;
using System.Collections;

namespace Ipop {
  /// <summary>
  /// A Generic Interface for Dht operations
  /// </summary>
  public interface IDht {
    /// <summary>
    /// Get Dht values
    /// </summary>    
    DhtGetResult[] Get(string key);

    /// <summary>
    /// Places value in Dht if it is a unique key
    /// </summary>    
    /// <returns>true if successful</returns>
    bool Create(string key, string value, int ttl);

    /// <summary>
    /// Places a value in Dht indexed by its key    
    /// <returns>true if successful</returns>
    bool Put(string key, string value, int ttl);

    /**
     * @return: token for ContinueGet
     */
    string BeginGet(string key);
    DhtGetResult ContinueGet(string token);
    void EndGet(string token);

    IDictionary GetDhtInfo();
  }
}
