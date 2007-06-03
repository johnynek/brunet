using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;
using CookComputing.XmlRpc;
using System;

namespace Ipop
{
	public class DhtServiceClient
	{
		public static SoapDht GetSoapDhtClient()
		{
			HttpChannel ch = new HttpChannel();
			ChannelServices.RegisterChannel(ch);
			RemotingConfiguration.RegisterWellKnownClientType(typeof(SoapDht),
		 		"http://127.0.0.1:64221/sd.rem");
			return new SoapDht();
		}

		public static IXmlRpcDht GetXmlDhtClient()
		{
            IXmlRpcDht proxy = (IXmlRpcDht)XmlRpcProxyGen.Create(typeof(IXmlRpcDht));
			proxy.Url = "http://127.0.0.1:64221/xd.rem";
			return proxy;
		}
	}
}
