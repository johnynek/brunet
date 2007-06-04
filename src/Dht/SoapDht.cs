using System;
using System.Collections;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Lifetime;
using System.Threading;
using Brunet;
using Brunet.Dht;

namespace Ipop {
  public class SoapDhtServer {
    public static Thread StartSoapDhtServerAsThread(FDht dht) {
      Thread DhtThread = new Thread(SoapDhtServer.StartSoapDhtServer);
      DhtThread.Start(dht);
      return DhtThread;
    }

    public static void StartSoapDhtServer(object odht) {
      FDht dht = (FDht) odht;
      ChannelServices.RegisterChannel(new TcpChannel(64221));
      SoapDht sd = new SoapDht(dht);
      RemotingServices.Marshal(sd, "sd.rem");

      ISponsor sponsor = new DhtLifeTimeSponsor();
      // get lifetime manager for the remote object
      ILease lifetime = (ILease)sd.GetLifetimeService();
      // registering our sponsor
      lifetime.Register(sponsor);

      while(true) System.Threading.Thread.Sleep(99999999);
    }
  }

  public class SoapDhtClient {
    public static SoapDht GetSoapDhtClient() {
      TcpChannel ch = new TcpChannel();
      ChannelServices.RegisterChannel(ch);
      RemotingConfiguration.RegisterWellKnownClientType(typeof(SoapDht),
          "tcp://127.0.0.1:64221/sd.rem");
      return new SoapDht();
    }
  }

  public class SoapDht : MarshalByRefObject {
    [NonSerialized]
    private FDht dht;

    public SoapDht(FDht dht) {
      this.dht = dht;
    }

    public SoapDht() {;}

    public string Create(string key, string value, string password, int ttl) {
      return DhtOp.Create(key, value, password, ttl, this.dht);
    }

    public string Put(string key, string value, string password, int ttl) {
      return DhtOp.Put(key, value, password, ttl, this.dht);
    }
    public Hashtable[] Get(string key) {
      return DhtOp.Get(key, this.dht);
    }
    public void PGet() {;}
  }

  public class DhtLifeTimeSponsor : ISponsor {
    public TimeSpan Renewal (ILease lease)
    {
      TimeSpan ts = new TimeSpan();
      ts = TimeSpan.FromDays(365);
      return ts;
    }
  }
}