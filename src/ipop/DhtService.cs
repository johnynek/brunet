using System;
using System.Collections;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Lifetime;
using CookComputing.XmlRpc;
using System.Threading;
using Brunet;
using Brunet.Dht;

namespace Ipop
{
    /// <summary>
    /// A Dht Server thread listens to Soap and XmlRpc requests
    /// Soap URL: http://loalhost:64221/sd.rem
    /// XmlRpc URL: http://loalhost:64221/xd.rem
    /// </summary>
    public class DhtServer
    {
        public static Thread StartDhtServerAsThread(FDht dht)
        {
            ServerProc server = new ServerProc(dht);
            Thread DhtThread = new Thread(new ThreadStart(server.Run));
            DhtThread.Start();
            return DhtThread;
        }

        /*
         * Ported to Mono 1.0.
         */
        private class ServerProc
        {
            private FDht dht;
            public ServerProc(FDht dht)
            {
                this.dht = dht;
            }

            public void Run()
            {
                this.Run(this.dht);
            }

            public void Run(object odht)
            {
                FDht dht = (FDht)odht;
                //modified by jx
                //ChannelServices.RegisterChannel(new TcpChannel(64221));                             
                XmlRpcServerFormatterSinkProvider chain = new XmlRpcServerFormatterSinkProvider();
                chain.Next = new SoapServerFormatterSinkProvider();

                IDictionary props = new Hashtable();
                props.Add("port", 64221);
                HttpChannel channel = new HttpChannel(props, null, chain);
                ChannelServices.RegisterChannel(channel);
                //modification ends

                SoapDht sd = new SoapDht(dht);
                RemotingServices.Marshal(sd, "sd.rem");
                //added by jx
                XmlRpcDht xd = new XmlRpcDht(dht);
                RemotingServices.Marshal(xd, "xd.rem");
                //modification ends

                ISponsor sponsor = new DhtLifeTimeSponsor();
                // get lifetime manager for the remote object
                ILease lifetime = (ILease)sd.GetLifetimeService();
                // registering our sponsor
                lifetime.Register(sponsor);

                while (true) System.Threading.Thread.Sleep(99999999);
            }
        }                
    }

    public class DhtLifeTimeSponsor : ISponsor
    {
        public TimeSpan Renewal(ILease lease)
        {
            TimeSpan ts = new TimeSpan();
            ts = TimeSpan.FromDays(365);
            return ts;
        }
    }
}