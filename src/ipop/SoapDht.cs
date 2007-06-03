using System;
using System.Text;
using System.Collections;
using Brunet.Dht;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;

namespace Ipop
{
    public class SoapDht : MarshalByRefObject, IDht
    {
        [NonSerialized]
        private FDht dht;

        public SoapDht(FDht dht)
        {
            this.dht = dht;
        }

        public SoapDht() { ;}

        public string Create(string key, string value, string password, int ttl)
        {            
            return DhtOp.Create(key, value, password, ttl, this.dht);
        }

        public string Put(string key, string value, string password, int ttl)
        {
            return DhtOp.Put(key, value, password, ttl, this.dht);
        }
        
        public DhtGetResultItem[] Get(string key)
        {
            Hashtable[] ht_res = DhtOp.Get(key, this.dht);
            ArrayList list = new ArrayList();
        	foreach(Hashtable re in ht_res)
        	{
        		list.Add(new DhtGetResultItem(re));
        	}
            return (DhtGetResultItem[])list.ToArray();
        }

        public void PGet() { ;}
    }

    public class SoapDhtClient
    {
        public static SoapDht GetSoapDhtClient()
        {
            HttpChannel ch = new HttpChannel();
            ChannelServices.RegisterChannel(ch);
            //modified by jx: tcp doesn't work fine with xmlrpc, so I changed the channel to http
            RemotingConfiguration.RegisterWellKnownClientType(typeof(SoapDht),
                "http://127.0.0.1:64221/sd.rem");
            return new SoapDht();
        }
    }
}
