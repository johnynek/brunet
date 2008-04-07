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

namespace Brunet.Rpc {
  /**
   * A proxy that acts between the XML-RPC client and Brunet RpcManager
   * It is also a IRpcHandler managed by RpcManager and handles calls 
   * register and unregister XML-RPC services from local node
   */
  public class XmlRpcManager : MarshalByRefObject, IRpcHandler {
    #region Fields
    [NonSerialized]
    private RpcManager _rpc;
    /**
     * Local Node
     */
    private Node _node;
    /**
     * Key: handler_name
     * Value: uri
     * Different from _rpc.method_handlers which is <handler_name, proxy>
     */
    [NonSerialized]
    private Hashtable _registered_xmlrpc = new Hashtable(); 
    #endregion

    public XmlRpcManager(Node node, RpcManager rpc) {
      _node = node;
      _rpc = rpc;
    }

    /**
     * @param node: target brunet node address
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
      Address target = AddressParser.Parse(node);
      AHSender s = new AHSender(_node, target, (ushort)ahOptions);
      return this.Proxy(s, maxResultsToWait, method, args);
    }

    /**
     * @param sender: URI specifying the sender.
     * @param maxResultToWait: When the synchronous call gets this amount of items, it returns even
     *                         if there are still more. (unless this argument is specified as a negative number)
     * @param method: brunet rpc method name
     * @param args: args of brunet rpc method
     * 
     * @return array of objects returned by the blocking queue
     */
    [XmlRpcMethod]
    public object[] uriproxy(string uri, int maxResultsToWait, string method, params object[] args) {
      ISender s = SenderFactory.CreateInstance(_node, uri);
      return this.Proxy(s, maxResultsToWait, method, args);
    }

    /**
     * @param relay: forwarding node brunet address.
     * @param init_option: options for the initial forwarding hop.
     * @param dest: destination node brunet address.
     * @ttl: maximum number of hops for between relay and destination.
     * @ahOptions: AH options to use between relay and destination. 
     * @param maxResultToWait: When the synchronous call gets this amount of items, it returns even
     *                         if there are still more. (unless this argument is specified as a negative number)
     * @param method: brunet rpc method name.
     * @param args: args of brunet rpc method.
     * 
     * @return array of objects returned by the blocking queue
     */
    [XmlRpcMethod]
    public object[] forwardingproxy(string relay, int init_option, string dest, int ttl, int ahOptions, int maxResultsToWait, string method, params object[] args) {
      Address forwarder = AddressParser.Parse(relay);
      Address target = AddressParser.Parse(dest);
      ForwardingSender s = new ForwardingSender(_node, forwarder, (ushort) init_option, target, (short) ttl, (ushort)ahOptions);
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
      if (results.Length > 0)
        return results[0];
      else
        return null;
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
    /**
     * Calls to this method come from Brunet, go to XML-RPC and return
     * to Brunet Overlay. So conversion needed from Adr->XmlRpc.Net->Adr
     */
    public object BrunetRpc2XmlRpc(params object[] args) {
      MethodBase mi = MethodBase.GetCurrentMethod();
      object ret;
      try {
        ret = this.Invoke(mi, AdrXmlRpcConverter.Adr2XmlRpc(args));
        return AdrXmlRpcConverter.XmlRpc2Adr(ret);
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
        //The actual method that the client wants to call is already specified in this.XmRpcMethod
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
    object[] proxy(string node, int ahOptions, int maxResultsToWait, string method, params object[] args);

    [XmlRpcMethod]
    object localproxy(string method, params object[] args);
  }

  public class XmlRpcManagerClient {
    /**
     * Log Request and Response Xml if logReqresp is set to true
     */
    public static IXmlRpcManager GetXmlRpcManager(string ip, int port, bool logReqresp) {
      IXmlRpcManager proxy = XmlRpcProxyGen.Create<IXmlRpcManager>();
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
        Debug.WriteLine(s);
        s = reader.ReadLine();
      }
    }
  }

  public class XmlRpcManagerServer {
    XmlRpcManager _xrm = null;
    RpcManager _rpc = null;
    Node _node;
    IChannel _channel;
 
    public XmlRpcManagerServer(int port) {
      IServerChannelSinkProvider chain = new XmlRpcServerFormatterSinkProvider();
      IDictionary props = new Hashtable();
      props.Add("port", port);
#if BRUNET_NUNIT
      /*
       * anonymous channel. In tests we don't care about service names and 
       * moreover, don't want system to complaint about serive name duplication
       * when it gets restarted frequently
       */
      props.Add("name", ""); 
#else
      props.Add("name", "xmlrpc");  //so that this channel won't collide with dht services
#endif
      _channel = new HttpChannel(props, null, chain);
      ChannelServices.RegisterChannel(_channel, false);
    }

    public void Stop()
    {
      try {
        Suspend();
      } catch{}
     
      try {
        ChannelServices.UnregisterChannel(_channel);
      } catch{}
    }

    public void Suspend()
    {
      RemotingServices.Disconnect(_xrm);
      _rpc.RemoveHandler("xmlrpc");
    }

    public void Update(Node node)
    {
      RpcManager rpc = RpcManager.GetInstance(node);
      Update(node, rpc);
    }

    /**
     * The method for now is used to allow RpcManager to be replaced
     * by MockRpcManager in unit tests
     */
    public void Update(Node node, RpcManager rpc) {
      _rpc = rpc;
      _node = node;
      _xrm = new XmlRpcManager(_node, _rpc);
      _rpc.AddHandler("xmlrpc", _xrm);
      RemotingServices.Marshal(_xrm, "xm.rem");
    }
  }

#if BRUNET_NUNIT
  class MockRpcManager : RpcManager {
    public class InvokeState {
      #region Fields
      private object[] _retvalues;  //set before invoke
      private object[] _params; //set by invoke
      private int _enqueue_interval_in_millisec = 10;
      private int _interval_between_last_enqueue_and_close = 10; 
      #endregion

      #region Properties
      public object[] RetValues {
        set {
          _retvalues = value;
        }
        get {
          return _retvalues;
        }
      }

      public object[] Params {
        get {
          return _params;
        }
        set {
          _params = value;
        }
      }

      public int EnqueueIntervalInMillisec {
        set {
          _enqueue_interval_in_millisec = value;
        }
        get {
          return _enqueue_interval_in_millisec;
        }
      }

      public int IntervalBetweenLastEnqueueAndClose {
        set {
          _interval_between_last_enqueue_and_close = value;
        }
        get {
          return _interval_between_last_enqueue_and_close;
        }
      }
      #endregion
    }

    #region Fields
    private static MockRpcManager _instance;
    //Only one invoke state active at any time
    public InvokeState CurrentInvokeState;
    #endregion

    protected MockRpcManager(ReqrepManager rrm) : base(rrm) { }

    public new static MockRpcManager GetInstance(Node node) {
      if(_instance == null) {
        ReqrepManager rrm = ReqrepManager.GetInstance(node);
        _instance = new MockRpcManager(rrm);
      }
      return _instance;
    }
    
    public override void Invoke(ISender target, Channel q, string method,
                              object[] args) {
      CurrentInvokeState.Params = args;

      Thread t = new Thread(this.AddStuffToBQAndClose);
      object[] state = new object[] { q, target };
      t.Start(state);
    }

    private void AddStuffToBQAndClose(object ostate) {
      object[] state = (object[])ostate;
      BlockingQueue bq = (BlockingQueue)state[0];
      ISender target = (ISender)state[1];

      foreach (object o in this.CurrentInvokeState.RetValues) {
        if (CurrentInvokeState.EnqueueIntervalInMillisec >= 0) {
          Thread.Sleep(CurrentInvokeState.EnqueueIntervalInMillisec);
        }
        RpcResult rs = new RpcResult(target, o);
        bq.Enqueue(rs);
      }

      if (CurrentInvokeState.IntervalBetweenLastEnqueueAndClose >= 0) {
        Thread.Sleep(CurrentInvokeState.IntervalBetweenLastEnqueueAndClose); 
      }
      bq.Close();
    }
  }

  [TestFixture]
  public class XmlRpcManagerTest {
    IXmlRpcManager _rpc;
    MockRpcManager _mrm;
    XmlRpcManagerServer _server;
    public readonly int Port = (new Random()).Next(10000, 65535);

    [TestFixtureSetUp]
    public void InitFixture() {
      ConsoleTraceListener myWriter = new ConsoleTraceListener();
      Debug.Listeners.Add(myWriter);
      _rpc = XmlRpcManagerClient.GetXmlRpcManager("127.0.0.1", Port, true);
      Node n = new StructuredNode(new AHAddress(new RNGCryptoServiceProvider()));
      _mrm = MockRpcManager.GetInstance(n);
      _server = new XmlRpcManagerServer(Port);
      _server.Update(n, _mrm);
      Debug.WriteLine(string.Format("Server started at {0}", Port));
    }

    [TestFixtureTearDown]
    public void TearDown() {
      _server.Stop();
    }
    
    [SetUp]
    public void InitTest() {
      _mrm.CurrentInvokeState = new MockRpcManager.InvokeState();
    }

    private string GetRandomNodeAddr() {
      AHAddress addr = new AHAddress(new RNGCryptoServiceProvider());
      return addr.ToString();
    }

    [Test]
    public void TestLocalproxyWithPrimitiveRetValAndMultipleParams() {
      _mrm.CurrentInvokeState.RetValues = new object[] { true };
      object ret = this._rpc.localproxy("Foo", "str1", "str2");
      Assert.AreEqual(true, (bool)ret);
      ret = this._rpc.localproxy("Foo");
      Assert.AreEqual(true, (bool)ret);
      ret = this._rpc.localproxy("Foo", "str1", 132);
      Assert.AreEqual(true, (bool)ret);
      ret = this._rpc.localproxy("Foo", new object[] { "str1", 1} );
      Assert.AreEqual(true, (bool)ret);
    }

    [Test]
    public void LocalProxyPassAndReturnStructs() {
      Hashtable tb = new Hashtable();
      tb.Add("key1", "value1");
      tb.Add("key2", "value2");
      XmlRpcStruct tb1 = new XmlRpcStruct();
      tb1.Add("key1", "value1");
      tb1.Add("key2", "value2");
      _mrm.CurrentInvokeState.RetValues = new object[] { tb };
      Hashtable actual = _rpc.localproxy("Foo", tb1) as Hashtable;
      Assert.AreEqual(tb["key1"], actual["key1"]);
      Assert.AreEqual(tb["key2"], actual["key2"]);
      actual = _rpc.localproxy("Foo", "arg1", tb1) as Hashtable;
      Assert.AreEqual(tb["key1"], actual["key1"]);
      Assert.AreEqual(tb["key2"], actual["key2"]);
    }

    [Test]
    public void LocalProxyReturnSimpleValue() {
      _mrm.CurrentInvokeState.RetValues = new object[] { null };
      string actual = _rpc.localproxy("Foo") as string;
      Assert.AreEqual(string.Empty, actual);
      _mrm.CurrentInvokeState.RetValues = new object[] { 4294967296 };
      actual = _rpc.localproxy("Foo") as string;
      Assert.AreEqual(Convert.ToString(4294967296) , actual);
    }

    [Test]
    public void LocalProxyReturnComplexValue() {
      _mrm.CurrentInvokeState.RetValues = new object[0];
      string actual = _rpc.localproxy("Foo") as string;
      Assert.AreEqual(string.Empty, actual);
      IList l = new ArrayList();
      l.Add(111);
      l.Add("string");
      _mrm.CurrentInvokeState.RetValues = new object[] { l };
      IList actual_l = _rpc.localproxy("Foo", "arg1") as IList;
      Assert.AreEqual(l[0], (int)actual_l[0]);
      Assert.AreEqual(l[1], actual_l[1] as string);
    }

    [Test]
    public void TestHashtableComplex() {
      Hashtable expected = new Hashtable();
      Hashtable e_key1 = new Hashtable();
      e_key1.Add("key1", "value1");
      //When use class as key, its ToString() string is put in XmlRpcStruct
      expected.Add(e_key1, e_key1);
      _mrm.CurrentInvokeState.RetValues = new object[] { expected };
      object ret = this._rpc.localproxy("Foo");
      Hashtable actual = (Hashtable)ret;      
      Hashtable ht_val = (Hashtable)actual["CookComputing.XmlRpc.XmlRpcStruct"];
      Assert.AreEqual("value1", ht_val["key1"]);
    }

    [Test]
    public void TestPrimitiveTypes() {
      ArrayList expected_values = new ArrayList();      
      MemBlock e_mb = MemBlock.Reference(Encoding.UTF8.GetBytes("test memblock"));
      expected_values.Add(e_mb);
      float e_f = (float)Math.PI;
      expected_values.Add((double)e_f);
      ushort e_sh = 11;
      expected_values.Add(e_sh);
      ulong e_long = 11111111111;
      expected_values.Add(e_long);
      uint e_ui = 34;
      expected_values.Add(e_ui);
      _mrm.CurrentInvokeState.RetValues = expected_values.ToArray();
      string target = this.GetRandomNodeAddr();
      object[] ret = this._rpc.proxy(target, 3, -1, "Foo");
      byte[] actual = (byte[])ret[0];
      Assert.IsTrue(e_mb.Equals(actual));
      float a_f = Convert.ToSingle((double)ret[1]);
      Assert.AreEqual(e_f, a_f);
    }

    [Test]
    [ExpectedException(typeof(XmlRpcFaultException))]
    public void TestWithAdrException() {
      AdrException ex = new AdrException(11111, new Exception());
      this._mrm.CurrentInvokeState.RetValues = new object[] { ex };
      this._rpc.localproxy("Foo");
    }

    [Test]
    [ExpectedException(typeof(XmlRpcFaultException))]
    public void TestWithOtherException() {
      Exception e = new ArgumentException("Testing message");
      this._mrm.CurrentInvokeState.RetValues = new object[] { e };
      this._rpc.localproxy("Foo");
    }
  }
#endif
}
