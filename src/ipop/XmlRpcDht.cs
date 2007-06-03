
using System;
using System.Runtime.Remoting;
using System.Text;
using System.Collections;
using Brunet;
using CookComputing.XmlRpc;
using Brunet.Dht;

namespace Ipop
{		
	/// <summary>
	/// Dht stub using XmlRpc protocol
	/// </summary>
    public class XmlRpcDht : MarshalByRefObject,IDht
	{			
        [NonSerialized]
        private FDht dht;

        public XmlRpcDht(FDht dht)
        {
            this.dht = dht;
        }
		
		[XmlRpcMethod] 
		public DhtGetResultItem[] Get(string key)
        {
#if SVC_TEST
            return new DhtGetResultItem[] { new DhtGetResultItem("xmlrpc",1000) };      
#endif
            Hashtable[] ht_res = DhtOp.Get(key, this.dht);
            ArrayList list = new ArrayList();
            foreach (Hashtable re in ht_res)
            {
                list.Add(new DhtGetResultItem(re));
            }
            return (DhtGetResultItem[])list.ToArray();
		}
		
		[XmlRpcMethod]
        public string Create(string key, string value, string password, int ttl) 
		{
            return DhtOp.Create(key, value, password, ttl, this.dht);
		}		
		
		[XmlRpcMethod]
        public string Put(string key, string value, string password, int ttl)
		{
            return DhtOp.Create(key, value, password, ttl, this.dht);
		}    
	}
}
