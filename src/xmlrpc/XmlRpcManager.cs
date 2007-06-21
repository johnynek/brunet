using System;
using System.Collections;
using System.Text;
using CookComputing.XmlRpc;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Threading;
using System.Security.Cryptography;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using Brunet.Dht;
using Brunet;
#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet {
  /**
   * A XmlRpc Proxy for RpcManager
   */
  public class XmlRpcManager : MarshalByRefObject {
    [NonSerialized]
    private RpcManager _rpc;

    public XmlRpcManager(RpcManager rpcm) {
      _rpc = rpcm;
    }
    
    /**
     * If maxResultsToWait is negative, then this argument is ignored
     */
    [XmlRpcMethod]
    public object[] proxy(string node, int ahOptions, int maxResultsToWait, string method, params object[] args) {      
      if (node.StartsWith("brunet:node:")) {
        node = node.Remove(0, 12); //remove "brunet:node:"
      }

      byte[] b_addr = Base32.Decode(node);
      AHAddress target = new AHAddress(b_addr);
      AHSender s = new AHSender(_rpc.Node, target, (ushort)ahOptions);
      BlockingQueue q = new BlockingQueue();      
      args = AdrXmlRpcConverter.XmlRpc2AdrParams(args);
      _rpc.Invoke(s, q, method, args);
      ArrayList allValues = new ArrayList();
      int counter = 0;
      try {
        do {
          RpcResult rpcRs = (RpcResult)q.Dequeue();
          object val = rpcRs.Result;
          Debug.WriteLine(string.Format("Original Result: {0}", val));
          object xmlrpc_val = AdrXmlRpcConverter.Adr2XmlRpc(val); //conversion in here
          counter++;
          allValues.Add(xmlrpc_val);
        } while (maxResultsToWait < 0 ? true : (counter < maxResultsToWait));
      } catch (Exception e) {
        Debug.WriteLine(e);
        if (e is AdrException) {
          object new_e = AdrXmlRpcConverter.Adr2XmlRpc(e);
          allValues.Add(new_e);
        }
      } finally {
        if (!q.Closed) {
          q.Close();
        }
      }      
      return allValues.ToArray();
    }

    /**
     * This object is intended to stay in memory
     */
    public override object InitializeLifetimeService() {
      ILease lease = (ILease)base.InitializeLifetimeService();
      if (lease.CurrentState == LeaseState.Initial) {
        lease.InitialLeaseTime = TimeSpan.Zero; //infinite lifetime
      }
      return lease;
    }

    //[XmlRpcMethod]
    //public object[] proxy(string node, string method, params object[] args) {
    //  return this.proxy(node, -1, method, args);
    //}
  }

  /**
   * Client proxy
   */
  public interface IXmlRpcManager : IXmlRpcProxy {
    [XmlRpcMethod]
    object[] proxy(string node, int ahOptions, int maxResultsToWait, string method, params object[] args);
    
    //[XmlRpcMethod]
    //object[] proxy(string node, string method, params object[] args);
  }

  public class XmlRpcManagerClient {
    /**
     * Log Request and Response Xml if logReqresp is set to true
     */
    public static IXmlRpcManager GetXmlRpcManager(bool logReqresp) {
      IXmlRpcManager proxy = (IXmlRpcManager)XmlRpcProxyGen.Create(typeof(IXmlRpcManager));
      proxy.Url = "http://127.0.0.1:10000/xm.rem";
      if (logReqresp) {
        XmlRpcManagerClientLogger logger = new XmlRpcManagerClientLogger();
        logger.Attach(proxy);
      }
      return proxy;
    }

    public static IXmlRpcManager GetXmlRpcManager() {
      return GetXmlRpcManager(false);
    }
  }

  public class XmlRpcManagerClientLogger : XmlRpcLogger {
    protected override void OnRequest(object sender, XmlRpcRequestEventArgs e) {
      base.OnRequest(sender, e);
      this.DumpStream(e.RequestStream);
    }

    protected override void OnResponse(object sender, XmlRpcResponseEventArgs e) {
      base.OnResponse(sender, e);
      this.DumpStream(e.ResponseStream);
    }

    private void DumpStream(Stream stm) {
      stm.Position = 0;
      TextReader reader = new StreamReader(stm);
      String s = reader.ReadLine();
      while (s != null) {
        Console.WriteLine(s);
        s = reader.ReadLine();
      }
    }
  }

  /**
   * For testing purpose. In production, XmlRpcManager could be deployed with existing server
   * port used: 10000
   */
  public class XmlRpcManagerServer {    

    public static void StartXmlRpcManagerServer(object oxm) {
      Debug.Listeners.Add(new ConsoleTraceListener());
      XmlRpcManager xm = (XmlRpcManager)oxm;            
      IServerChannelSinkProvider chain = new XmlRpcServerFormatterSinkProvider();
      IDictionary props = new Hashtable();
      props.Add("port", 10000);
      props.Add("name", "xmlrpc");  //so that this channel won't collide with dht services
      HttpChannel channel = new HttpChannel(props, null, chain);
      ChannelServices.RegisterChannel(channel);
      RemotingServices.Marshal(xm, "xm.rem");
      /*
       * No need to setup Sponsor for testing
       */
      System.Threading.Thread.Sleep(Timeout.Infinite);
    }
  }
  
  /**
   * This class is used to test the XmlRpcManager features
   * Intended to be used in a single thread
   */
#if BRUNET_NUNIT
  class MockRpcManager : RpcManager {
    protected MockRpcManager(ReqrepManager rrm) : base(rrm) { }
    
    private object[] _retvalues;

    private object[] _params;

    /**
     * Singletoin instance is used here to set the status of the object 
     * prior to calling from the client
     */
    private static MockRpcManager _instance;

    /**
     * Use this Property to set the return value before fire Invoke
     */
    public object[] RetValsOfInvoke {
      get { return this._retvalues; }
      set { this._retvalues = value; }
    }

    public object[] ParamsOfInvoke {
      get { return this._params; }
      set { this._params = value; }
    }

    /**
     * Use this method instead of RpcManager.GetInstance(Node) to init.
     * A StructuredNode with random address is used inside. It's useless anyway.
     */
    public static MockRpcManager GetInstance() {
      if(_instance == null) {
        Node n = new StructuredNode(new AHAddress(new RNGCryptoServiceProvider()));
        ReqrepManager rrm = ReqrepManager.GetInstance(n);
        _instance = new MockRpcManager(rrm);
      }
      return _instance;
    }
    
    public override void Invoke(ISender target, BlockingQueue q, string method,
                              params object[] args) {      
      if (this._retvalues == null) {
        throw new InvalidOperationException("Should use RetValsOfInvoke to set the return value first");
      }

      this._params = args;

      Thread t = new Thread(this.AddStuffToBQ);
      object[] state = new object[] { q, target };
      t.Start(state);
    }

    private void AddStuffToBQ(object ostate) {
      object[] state = (object[])ostate;
      BlockingQueue bq = (BlockingQueue)state[0];
      ISender target = (ISender)state[1];
      foreach (object o in this._retvalues) {
        RpcResult rs = new RpcResult(target, o);
        bq.Enqueue(rs);
      }      
      //At least wait for a while for the caller to enter the waiting state
      System.Threading.Thread.Sleep(50);
      bq.Close();
    }
  }

  [TestFixture]
  public class XmlRpcManagerTest {
    IXmlRpcManager _rpc;
    MockRpcManager _mrm;
    Thread _server;

    [TestFixtureSetUp]
    public void InitFixture() {
      _rpc = XmlRpcManagerClient.GetXmlRpcManager(true);
      _mrm = MockRpcManager.GetInstance();
      XmlRpcManager xm = new XmlRpcManager(_mrm);
      _server = new Thread(XmlRpcManagerServer.StartXmlRpcManagerServer);
      _server.Start(xm);
    }

    [SetUp]
    public void InitTest() {
      this._mrm.ParamsOfInvoke = null;
    }

    private string GetRandomNodeAddr() {
      AHAddress addr = new AHAddress(new RNGCryptoServiceProvider());
      return addr.ToString();
    }

    /**
     * Would be the simplest case
     */
    [Test]    
    [Ignore]
    public void TestBoolRetVal_StringParams() {
      _mrm.RetValsOfInvoke = new object[] { true };
      string node = this.GetRandomNodeAddr();
      object[] ret = this._rpc.proxy(node, 0, -1, "IgnoredMethodName", "string1", "string2");
      Assert.AreEqual(1, ret.Length);
      Assert.AreEqual(true, ret[0]);
    }

    [Test] 
    [Ignore]
    public void TestHashtableOfStringsRetVal_StringParams() {
      Hashtable ht = new Hashtable();
      ht.Add("key1", "value1");
      ht.Add("key2", "value2");
      _mrm.RetValsOfInvoke = new object[] { ht };
      string node = this.GetRandomNodeAddr();
      object[] ret = this._rpc.proxy(node,0, -1, "IgnoredMethodName", "string1", "string2");
      Assert.AreEqual(1, ret.Length);
      Hashtable actual = (Hashtable)ret[0];
      Assert.AreEqual(2, actual.Count);
      Assert.AreEqual("value1", actual["key1"]);
      Assert.AreEqual("value2", actual["key2"]);
    }

    [Test]
    [Ignore]
    public void TestHashtableComplex() {
      Hashtable expected = new Hashtable();
      Hashtable e_key1 = new Hashtable();
      e_key1.Add("key1", "value1");
      expected.Add(e_key1, e_key1);
      _mrm.RetValsOfInvoke = new object[] { expected };
      string node = this.GetRandomNodeAddr();
      object[] ret = this._rpc.proxy(node, 0, -1, "IgnoredMethodName", "string1", "string2");
      Assert.AreEqual(1, ret.Length);
      Hashtable actual = (Hashtable)ret[0];      
      Hashtable ht_val = (Hashtable)actual["CookComputing.XmlRpc.XmlRpcStruct"];
      Assert.AreEqual("value1", ht_val["key1"]);
    }

    [Test]
    [Ignore]
    public void TestPrimitiveTypes() {
      ArrayList expected_values = new ArrayList();      
      MemBlock e_mb = MemBlock.Reference(Encoding.UTF8.GetBytes("test memblock"));
      expected_values.Add(e_mb);
      float e_f = (float)Math.PI;
      object o = (double)e_f;      
      expected_values.Add(e_f);
      ushort e_sh = 11;
      expected_values.Add(e_sh);
      ulong e_long = 11111111111;
      expected_values.Add(e_long);
      uint e_ui = 34;
      expected_values.Add(e_ui);
      _mrm.RetValsOfInvoke = expected_values.ToArray();
      string node = this.GetRandomNodeAddr();
      object[] ret = this._rpc.proxy(node, 0, -1, "IgnoredMethodName", "string1", "string2");
      byte[] actual = Convert.FromBase64String((string)ret[0]);
      Assert.IsTrue(e_mb.Equals(actual));
      float a_f = Convert.ToSingle((double)ret[1]);
      Assert.AreEqual(e_f, a_f);
    }

    [Test]
    [Ignore]
    public void TestListRetValue() {
      IList expected = new ArrayList();
      expected.Add("string");
      Hashtable ht = new Hashtable();
      ht.Add("key1", "data1");
      expected.Add(ht);
      _mrm.RetValsOfInvoke = new object[] { expected };
      string node = this.GetRandomNodeAddr();
      object[] ret = this._rpc.proxy(node, 0, -1, "IgnoredMethodName", "string1", "string2");
    }

    [Test]
    [Ignore]
    public void TestDhtGet() {
      object[] expected_ret = new object[0];
      _mrm.RetValsOfInvoke = expected_ret;
      string node = this.GetRandomNodeAddr();
      object[] ret = this._rpc.proxy(node, 0, -1, "dht.Get", Encoding.UTF8.GetBytes("key1"), 1000, Encoding.UTF8.GetBytes(""));
      object[] args = this._mrm.ParamsOfInvoke;
      Assert.AreEqual(3, args.Length);
      object o = args[2];
      Console.WriteLine(o.GetType());
      MemBlock mb = (MemBlock)o;
      Console.WriteLine("MB: {0}", mb.ToBase32String());
    }
  }
#endif
}
