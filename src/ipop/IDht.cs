using System;
using System.Text;
using Brunet.Dht;

namespace Ipop
{
    /// <summary>
    /// A Generic Interface for Dht operations
    /// </summary>
    public interface IDht
    {
        /// <summary>
        /// Get Dht values
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        DhtGetResultItem[] Get(string key);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="password"></param>
        /// <param name="ttl"></param>
        /// <returns>Returns a password if it works or NULL if it didn't</returns>
        string Create(string key, string value, string password, int ttl);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="password"></param>
        /// <param name="ttl"></param>
        /// <returns>The password used in this Put operation</returns>
        string Put(string key, string value, string password, int ttl);
    }
}
