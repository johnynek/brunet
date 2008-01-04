/**
 * This file contains essential classes for xml rpc->brunet rpc bridge.
 * Console output comes with DEBUG configure and won't happen in release version.
 */
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
#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet {
  /**
   * A proxy that acts between the XML-RPC client and Brunet RpcManager
   * It is also a IRpcHandler managed by RpcManager and handles calls 
   * register and unregister XML-RPC services from local node
   */
  public class XmlRpcManager : MarshalByRefObject, IRpcHandler {
    [NonSerialized]
    private RpcManager _rpc;
    private Node _node;
    /**
     * Key: handler_name
     * Value: uri
     * Different the counterpart from _rpc.method_handlers which is <handler_name, proxy>
     */
    [NonSerialized]
    private Hashtable _registered_xmlrpc = new Hashtable();

    public XmlRpcManager(Node node, RpcManager rpc) {
      _node = node;
      _rpc = rpc;
    }

    /**
     * @param node: brunet node address
     * @param ahOptions: AHOptions, @see AHPacket.AHOptions
     * @param maxResultToWait: When the synchronous call gets this amount of items, it returns even
     *                         if there are still more. (unless this argument is specified as a negative number)
     * @param method: brunet rpc method name
     * @param args: args of brunet rpc method
     * 
     * @return array of objects returned by the blocking queue
     */
    [XmlRpcMethod]
    public object[] proxy(string node, int ahOptions, int maxResultsToWait, string method, params object[] args) {
      if (node.StartsWith("brunet:node:")) {
        node = node.Remove(0, 12); //remove "brunet:node:"
      }

      byte[] b_addr = Base32.Decode(node);
      AHAddress target = new AHAddress(b_addr);
      AHSender s = new AHSender(_node, target, (ushort)ahOptions);
      return this.Proxy(s, maxResultsToWait, method, args);
    }

    /**
     * Similar to proxy but it takes the local node as the target and
     * only waits for the first incoming value and return
     * @return the first incoming result
     */
    [XmlRpcMethod]
    public object localproxy(string method, params object[] args) {
      object[] results = this.Proxy(_node, 1, method, args);
      //it can't be an empty array so we don't check array length here
      return results[0];
    }

    private object[] Proxy(ISender sender,int maxResultsToWait, string method, object[] args) {
      BlockingQueue q = new BlockingQueue();
      args = AdrXmlRpcConverter.XmlRpc2AdrParams(args);
      _rpc.Invoke(sender, q, method, args);
      ArrayList allValues = new ArrayList();
      int counter = 0;
      ISender rsSender = null;
      try {
        do {
          RpcResult rpcRs = (RpcResult)q.Dequeue();
          rsSender = rpcRs.ResultSender;  //get it before exception thrown
          object val = rpcRs.Result;
          Debug.WriteLine(string.Format("Original Result: {0}", val));
          object xmlrpc_val = AdrXmlRpcConverter.Adr2XmlRpc(val); //conversion in here
          counter++;
          allValues.Add(xmlrpc_val);
        } while (maxResultsToWait < 0 ? true : (counter < maxResultsToWait));
      } catch (Exception e) {
        Debug.WriteLine(e);
        string s = string.Empty;
        if (e is AdrException) {
          if (rsSender != null) {
            s = AdrXmlRpcConverter.Adr2XmlRpc(rsSender) as string;
          }
        }
        if (e is InvalidOperationException) {
          /*
           * this is what we expect at the end of Dequeuing, so just return what we've gotten so far
           * it could be an empty array
           */
          return allValues.ToArray();
        }
        Exception new_e = AdrXmlRpcConverter.Adr2XmlRpc(e) as Exception;
        throw new Exception(new_e.Message + 
          (s.Equals(string.Empty) ? string.Empty : string.Format("thrown by: {0}", s)));
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

    public void AddAsRpcHandler() {
      _rpc.AddHandler("xmlrpc",this);
    }

    /**
     * Accepts BrunetRpc calls but not XmlRpc calls
     */
    public void AddXRHandler(string handler_name, string uri) {
      XmlRpcHandler handler = new XmlRpcHandler(uri, _rpc);
      _rpc.AddHandler(handler_name, handler);
      _registered_xmlrpc.Add(handler_name, uri);
    }

    /**
     * Accepts BrunetRpc calls but not XmlRpc calls
     */
    public void RemoveXRHandler(string handler_name, string uri) {
      string expected_uri = _registered_xmlrpc[handler_name] as string;
      if (string.IsNullOrEmpty(expected_uri)) {
        throw new Exception("There is no xmlrpc proxy with the specified handler name already in RpcManager");
      } else if(!expected_uri.Equals(uri)) {
        throw new Exception("Uri doesn't match with the proxy already registered");
      }
      _rpc.RemoveHandler(handler_name);
    }

    #region IRpcHandler Members

    public void HandleRpc(ISender caller, string method, IList args, object rs) {
      if (method.Equals("AddXRHandler") || method.Equals("RemoveXRHandler")) {
        ReqrepManager.ReplyState s = (ReqrepManager.ReplyState)caller;
        ISender sender = s.ReturnPath;
        if (Object.ReferenceEquals(_node, sender)) {
          if (args.Count == 2) {
            if (method.Equals("AddXRHandler"))
              this.AddXRHandler(args[0] as string, args[1] as string);
            else
              this.RemoveXRHandler(args[0] as string, args[1] as string);
            _rpc.SendResult(rs, null);
            return;
          } else {
            throw new ArgumentException("2 arguments expected");
          }
        } else {
          throw new AdrException(-32602, "This operation is only accessible for local calls");
        }
      } else {
        object result = null;
        try {
          Type type = this.GetType();
          MethodInfo mi = type.GetMethod(method);
          object[] arg_array = new object[args.Count];
          args.CopyTo(arg_array, 0);
          result = mi.Invoke(this, arg_array);
        } catch (Exception e) {
          result = new AdrException(-32602, e);
        }
        _rpc.SendResult(rs, result);
      }
    }
    #endregion
  }
  
  public class XmlRpcHandler : XmlRpcClientProtocol, IRpcHandler {
    private RpcManager _rpc;

    public XmlRpcHandler(string url, RpcManager rpc) {
      this.Url = url;
      _rpc = rpc;
    }

    public void AttachLogger() {
      XmlRpcManagerClientLogger logger = new XmlRpcManagerClientLogger();
      logger.Attach(this);
    }
    
    [XmlRpcMethod]
    public object BrunetRpc2XmlRpc(params object[] args) {
      MethodBase mi = MethodBase.GetCurrentMethod();
      object ret;
      try {
        ret = this.Invoke(mi, args);
        return ret;
      } catch (Exception e) {
        Debug.WriteLine(e);
        throw e;
      }
    }

    #region IRpcHandler Members

    public void HandleRpc(ISender caller, string method, IList args, object rs) {
      this.XmlRpcMethod = method;
      object result = null;
      try {
        object[] arg_array = new object[args.Count];
        args.CopyTo(arg_array, 0);
        //no method what method is specified, we just call this only method in this class
        result = BrunetRpc2XmlRpc(arg_array);
      } catch (Exception e) {
        result = new AdrException(-32602, e);
      }
      _rpc.SendResult(rs, result);
    }

    #endregion
  }


  /**
   * Client proxy
   */
  public interface IXmlRpcManager : IXmlRpcProxy {
    [XmlRpcMethod]
    object[] proxy(string node, int ahOptions, int maxResultsToWait, string method, object[] args);

    [XmlRpcMethod]
    object[] localproxy(string method, object[] args);
  }

  public class XmlRpcManagerClient {
    /**
     * Log Request and Response Xml if logReqresp is set to true
     */
    public static IXmlRpcManager GetXmlRpcManager(string ip, int port, bool logReqresp) {
      IXmlRpcManager proxy = (IXmlRpcManager)XmlRpcProxyGen.Create(typeof(IXmlRpcManager));
      proxy.Url = "http://" + ip + ":" + port + "/xm.rem";
      if (logReqresp) {
        XmlRpcManagerClientLogger logger = new XmlRpcManagerClientLogger();
        logger.Attach(proxy);
      }
      return proxy;
    }

    public static IXmlRpcManager GetXmlRpcManager(string ip, int port) {
      return GetXmlRpcManager(ip, port, false);
    }

    public static IXmlRpcManager GetXmlRpcManager(int port) {
      return GetXmlRpcManager("127.0.0.1", port, false);
    }

    public static IXmlRpcManager GetXmlRpcManager(bool logReqresp) {
      return GetXmlRpcManager("127.0.0.1", 10000, logReqresp);
    }

    public static IXmlRpcManager GetXmlRpcManager() {
      return GetXmlRpcManager("127.0.0.1", 10000, false);
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
   * port used: 10000
   */
  public class XmlRpcManagerServer {
    XmlRpcManager _xrm = null;
    RpcManager _rpc = null;
    Node _node;
    public XmlRpcManagerServer(int port) {
      IServerChannelSinkProvider chain = new XmlRpcServerFormatterSinkProvider();
      IDictionary props = new Hashtable();
      props.Add("port", port);
      props.Add("name", "xmlrpc");  //so that this channel won't collide with dht services
      HttpChannel channel = new HttpChannel(props, null, chain);
      ChannelServices.RegisterChannel(channel);
    }

    public void Stop()
    {
      RemotingServices.Disconnect(_xrm);
      _rpc.RemoveHandler("xmlrpc");
    }

    public void Update(Node node)
    {
      _rpc = RpcManager.GetInstance(node);
      _node = node;
      _xrm = new XmlRpcManager(_node, _rpc);
      _rpc.AddHandler("xmlrpc",_xrm);
      RemotingServices.Marshal(_xrm, "xm.rem");
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
                              object[] args) {      
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
    [Ignore]
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
      object[] ret = this._rpc.proxy(node, 0, -1, "IgnoredMethodName", new object[] {"string1", "string2"});
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
      object[] ret = this._rpc.proxy(node,0, -1, "IgnoredMethodName", new object[] {"string1", "string2"});
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
      object[] ret = this._rpc.proxy(node, 0, -1, "IgnoredMethodName", new object[] {"string1", "string2"});
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
      object[] ret = this._rpc.proxy(node, 0, -1, "IgnoredMethodName", new object[] {"string1", "string2"});
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
      object[] ret = this._rpc.proxy(node, 0, -1, "IgnoredMethodName", new object[] {"string1", "string2"});
    }

    [Test]
    [Ignore]
    public void TestDhtGet() {
      object[] expected_ret = new object[0];
      _mrm.RetValsOfInvoke = expected_ret;
      string node = this.GetRandomNodeAddr();
      object[] ret = this._rpc.proxy(node, 0, -1, "dht.Get", new object[] { Encoding.UTF8.GetBytes("key1"), 1000, Encoding.UTF8.GetBytes("") });
      object[] args = this._mrm.ParamsOfInvoke;
      Assert.AreEqual(3, args.Length);
      object o = args[2];
      MemBlock mb = (MemBlock)o;
      Console.WriteLine("MB: {0}", mb.ToBase32String());
    }

    [Test]
    [Ignore]
    public void TestWithException() {
      string node = this.GetRandomNodeAddr();
      AdrException ex = new AdrException(11111, new Exception());
      this._mrm.RetValsOfInvoke = new object[] { ex };
      object[] ret = this._rpc.proxy(node, 0, -1, "dht.Get", new object[0]);
    }
  }
#endif
}
