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
using System.Collections.Generic;
#if BRUNET_NUNIT
using NUnit.Framework;
#endif

using Brunet.Messaging;
using Brunet.Concurrent;
using Brunet.Symphony;
using Brunet.Util;
namespace Brunet.Services.XmlRpc {
  /**
   * A proxy that acts between the XML-RPC client and Brunet RpcManager.
   * It is also a IRpcHandler managed by RpcManager that handles calls that
   * register and unregister XML-RPC services for local node
   */
  public class XmlRpcManager : MarshalByRefObject, IRpcHandler {
    #region Fields
    [NonSerialized]
    private readonly RpcManager _rpc;
    /**
     * Local Node
     */
    private readonly Node _node;
    /**
     * Key: handler_name
     * Value: uri
     * Different from _rpc.method_handlers which is <handler_name, proxy>
     */
    [NonSerialized]
    private readonly Hashtable _registered_xmlrpc = new Hashtable(); 
    #endregion

    public XmlRpcManager(Node node, RpcManager rpc) {
      _node = node;
      _rpc = rpc;
    }

    /**
     * @param node target brunet node address
     * @param ahOptions AHOptions, @see AHPacket.AHOptions
     * @param maxResultToWait When the synchronous call gets this amount of 
     * items, it returns even if there are still more. (unless this argument is specified
     * as a negative number)
     * @param method brunet rpc method name
     * @param args args of brunet rpc method
     * 
     * @return array of objects returned by the blocking queue
     */
    [XmlRpcMethod]
    public object[] proxy(string node, int ahOptions, int maxResultsToWait, 
        string method, params object[] args) {
      Address target = AddressParser.Parse(node);
      AHSender s = new AHSender(_node, target, (ushort)ahOptions);
      return this.Proxy(s, maxResultsToWait, method, args);
    }

    /**
     * @param sender URI specifying the sender.
     * @param maxResultToWait When the synchronous call gets this amount of items, it returns even
     *                         if there are still more. (unless this argument is specified as a negative number)
     * @param method brunet rpc method name
     * @param args args of brunet rpc method
     * 
     * @return array of objects returned by the blocking queue
     * @see Brunet.SenderFactory
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
     * @param method brunet rpc method name.
     * @param args args of brunet rpc method.
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
      BlockingQueue q = new BlockingQueue(maxResultsToWait);
      args = AdrXmlRpcConverter.XmlRpc2AdrParams(args);
      _rpc.Invoke(sender, q, method, args);
      ArrayList allValues = new ArrayList();
      int counter = 0;
      ISender rsSender = null;
      try {
        do {
          rsSender = null; //Reset it before the following:
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
     * Sets the object to have infinite lifetime because we want it to stay in the 
     * memory.
     */
    public override object InitializeLifetimeService() {
      ILease lease = (ILease)base.InitializeLifetimeService();
      if (lease.CurrentState == LeaseState.Initial) {
        lease.InitialLeaseTime = TimeSpan.Zero; //infinite lifetime
      }
      return lease;
    }

    /**
     * Adds the instance as a RpcHandler under the name space "xmlrpc".
     */
    public void AddAsRpcHandler() {
      _rpc.AddHandler("xmlrpc",this);
    }

    /**
     * Registers an XML-RPC service as a Brunet Rpc handler.
     * @param handler_name the namespace used to register the handler with 
     * RpcManager.
     * @param uri the URI at which the XML-RPC service could be accessed.
     */
    public void AddXRHandler(string handler_name, string uri) {
      XmlRpcHandler handler = new XmlRpcHandler(uri, _node);
      _rpc.AddHandler(handler_name, handler);
      _registered_xmlrpc.Add(handler_name, uri);
    }

    /**
     * Removes the XML-RPC service as a Brunet Rpc handler.
     * @param handler_name the namespace used to register the handler with 
     * RpcManager.
     * @param uri the URI at which the XML-RPC service could be accessed.
     */
    public void RemoveXRHandler(string handler_name, string uri) {
      string expected_uri = _registered_xmlrpc[handler_name] as string;
      if (string.IsNullOrEmpty(expected_uri)) {
        throw new Exception("There is no xmlrpc proxy with the specified handler name already in RpcManager");
      } else if(!expected_uri.Equals(uri)) {
        throw new Exception("Uri doesn't match with the proxy already registered");
      }
      _rpc.RemoveHandler(handler_name);
      _registered_xmlrpc.Remove(handler_name);
    }

    #region IRpcHandler Members
    /**
     * Handles RPC calls.
     * Note that AddXRHandler and RemoveXRHandler calls are only accepted when
     * they are made by local Brunet node.
     */
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
  
  /**
   * Handles calls from Brunet Rpc, invokes the XML-RPC service that this
   * handler represents and returns the results to Brunet Rpc.
   */
  public class XmlRpcHandler : IRpcHandler {
    private readonly RpcManager _rpc;
    private readonly Node _node;
    private readonly string _url;

    /**
     * @param url the URL of the XML-RPC service.
     * @param rpc The XmlRpcManager instance that the handler uses.
     */
    public XmlRpcHandler(string url, Node node) {
      _url = url;
      _node = node;
      _rpc = _node.Rpc;
    }

#if BRUNET_NUNIT
    /**
     * Where we don't need RpcManager...
     */
    public XmlRpcHandler(string url) {
      this._url = url;
    }
#endif

    /**
     * Enables the logging output.
     */
    public void AttachLogger() {
      //XmlRpcManagerClientLogger logger = new XmlRpcManagerClientLogger();
      //logger.Attach(this);
    }
    
    /**
     * Fires XML-RPC call and gets the job done, then returns Brunet Rpc result.
     * 
     * Calls to this method come from Brunet, go to XML-RPC and return
     * to Brunet Overlay. So conversion needed from Adr->XmlRpc.Net->Adr
     */
    public void BrunetRpc2XmlRpc(object xmlrpcCallState) {
      XmlRpcCallState state = (XmlRpcCallState)xmlrpcCallState;
      object ret = null;
      try {
        ret = state.XmlRpcCall(state.MethodArgs);
        ret = AdrXmlRpcConverter.XmlRpc2Adr(ret);
      } catch (Exception e) {
        Debug.WriteLine(e);
        ret = new AdrException(-32602, e);
      } finally {
        _node.EnqueueAction(new RpcSendResultAction(_rpc, state.RequestState, ret));
      }
    }

    #region IRpcHandler Members
    /**
     * Asynchronously Invokes the method on the XML-RPC service.
     */
    public void HandleRpc(ISender caller, string method, IList args, object rs) {
      object[] arg_array = new object[args.Count];
      args.CopyTo(arg_array, 0);
      XmlRpcCallState state = new XmlRpcCallState(_url, method, arg_array, rs);
      ThreadPool.QueueUserWorkItem(new WaitCallback(BrunetRpc2XmlRpc), state);
    }
    #endregion

    class XmlRpcCallState : XmlRpcClientProtocol {
      public readonly object[] MethodArgs;
      public readonly object RequestState;
      public XmlRpcCallState(string uri, string meth, object[] args, object rs) {
        this.Url = uri;
        this.XmlRpcMethod = meth;
        args = (object[])AdrXmlRpcConverter.Adr2XmlRpc(args);
        MethodArgs = args;
        RequestState = rs;
      }
      /**
       * Represents the remote method.
       * @exception Whatever caught from XML-RPC is thrown.
       */
      [XmlRpcMethod]
      public object XmlRpcCall(params object[] args) {
        MethodBase mi = MethodBase.GetCurrentMethod();
        //Without the cast, XML-RPC.NET throws exception.
        return this.Invoke(mi, (object)args);
      }
      
    }

    class RpcSendResultAction : IAction {
      private readonly RpcManager _rpc;
      private readonly object _rs;
      private readonly object _result;
      
      public RpcSendResultAction(RpcManager rpc, object rs, object result) {
        _rpc = rpc; 
        _rs = rs;
        _result = result;
      }

      public void Start() {
        _rpc.SendResult(_rs, _result);
      }
    }

  }


  /**
   * Client proxy interface.
   */
  public interface IXmlRpcManager : IXmlRpcProxy {
    [XmlRpcMethod]
    object[] proxy(string node, int ahOptions, int maxResultsToWait, string method, params object[] args);

    [XmlRpcMethod]
    object localproxy(string method, params object[] args);
  }

  public class XmlRpcManagerClient {
    /**
     * Creates an XmlRpcManager instance with the specified ip, port.
     * Logs Request and Response messages in XML if logReqresp is set to true
     */
    public static IXmlRpcManager GetXmlRpcManager(string ip, int port, 
      string relativeUri, bool logReqresp) {
      IXmlRpcManager proxy = XmlRpcProxyGen.Create<IXmlRpcManager>();
      proxy.Url = string.Format("http://{0}:{1}/{2}", ip, port, relativeUri);
      if (logReqresp) {
        XmlRpcManagerClientLogger logger = new XmlRpcManagerClientLogger();
        logger.Attach(proxy);
      }
      return proxy;
    }
  }

  /**
   * Logs the request and response messages.
   */
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

  /// <summary>
  /// .NET Remoting service that hosts the XmlRpcManager instances.
  /// </summary>
  public class XmlRpcManagerServer {
    readonly IChannel _channel;
    readonly Dumper _dumper;
    readonly object _sync_root = new object();

    /// <summary>
    /// (Node, XmlRpcManager) mapping.
    /// </summary>
    readonly IDictionary<Node, XmlRpcManager> _xrm_mappings = 
      new Dictionary<Node, XmlRpcManager>();

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlRpcManagerServer"/> class 
    /// and registers it at the specified port on local machine.
    /// </summary>
    /// <param name="port">The port.</param>
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
      props.Add("name", "xmlrpcmanagers");  //so that this channel won't collide with dht services
#endif
      _channel = new HttpChannel(props, null, chain);
      ChannelServices.RegisterChannel(_channel, false);

      _dumper = new Dumper(this);
      RemotingServices.Marshal(_dumper, "xmserver.rem");
    }

    /// <summary>
    /// Stops this server.
    /// </summary>
    public void Stop()
    {
      RemotingServices.Disconnect(_dumper);
      try {
        RemoveAll();
      } catch{}
     
      try {
        ChannelServices.UnregisterChannel(_channel);
      } catch{}
    }

    /// <summary>
    /// Suspends the service objects.
    /// </summary>
    public void RemoveAll() {
      lock (_sync_root) {
        foreach (var pair in _xrm_mappings) {
          RemotingServices.Disconnect(pair.Value);
          pair.Key.Rpc.RemoveHandler("xmlrpc");
        }
        _xrm_mappings.Clear();
      }
    }

    /// <summary>
    /// Adds the specified node to listen to XMLRPC proxy calls.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <remarks>Uses a node's string version of Brunet address plus ".rem" as the 
    /// relative Uri.</remarks>
    public virtual void Add(Node node) {
      Add(node, string.Format("{0}.rem", node.Address.ToString()));
    }

    /// <summary>
    /// Stops the specified node from listening to XMLRPC proxy calls.
    /// </summary>
    /// <param name="node">The node.</param>
    public void Remove(Node node) {
      lock (_sync_root) {
        RemotingServices.Disconnect(_xrm_mappings[node]);
        node.Rpc.RemoveHandler("xmlrpc");
        _xrm_mappings.Remove(node);
        CheckAndSetDefaultManager();
      }
    }

    /// <summary>
    /// Allows nodes to be registered by whatever URI the user provides
    /// </summary>
    /// <param name="node"></param>
    /// <param name="uri"></param>
    public void Add(Node node, string uri) {
      var xrm = new XmlRpcManager(node, node.Rpc);
      lock (_sync_root) {
        // throw an exception if this mapping exists...
        _xrm_mappings.Add(node, xrm);
        RemotingServices.Marshal(xrm, uri);
        CheckAndSetDefaultManager();
      }
      // We only add this if there is no exception above...
      node.Rpc.AddHandler("xmlrpc", xrm);
    }

#if BRUNET_NUNIT
    /// <summary>
    /// The overloaded method for now is used to allow RpcManager to be replaced
    /// by MockRpcManager in unit tests.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="rpc"></param>
    /// <param name="uri"></param>
    internal void Add(Node node, RpcManager rpc, string uri) {
      var xrm = new XmlRpcManager(node, rpc);
      lock (_sync_root) {
        // throw an exception if this mapping exists...
        _xrm_mappings.Add(node, xrm);
        RemotingServices.Marshal(xrm, uri);
        CheckAndSetDefaultManager();
      }
      rpc.AddHandler("xmlrpc", xrm);
    }
#endif

    /// <summary>
    /// Checks and sets the default XmlRpcManager. Should only be called by Add 
    /// and Remove.
    /// </summary>
    void CheckAndSetDefaultManager() {
        if (_xrm_mappings.Count == 1) {
          // Add an alias for the first node.
          foreach(var pair in _xrm_mappings) {
            RemotingServices.Marshal(pair.Value, "xm.rem");
            // Since only one pair, break here.
            break;
          }
      }
    }

    /// <summary>
    /// Responsible for dumping server information to XML-RPC clients.
    /// </summary>
    public class Dumper : MarshalByRefObject {
      readonly XmlRpcManagerServer _outer;

      internal Dumper(XmlRpcManagerServer outer) {
        _outer = outer;
      }

      /// <summary>
      /// Dumps a list of nodes registered with the server. The URI of a node's 
      /// corresponding XmlRpcManager is "{address}.rem"
      /// </summary>
      /// <returns>The list of node addresses.</returns>
      [XmlRpcMethod("listNodes")]
      public string[] ListNodes() {
        var ret = new ArrayList();
        lock (_outer._sync_root) {
          foreach (var pair in _outer._xrm_mappings) {
            ret.Add(pair.Key.Address.ToString());
          }
        }
        return ret.ToArray(typeof(string)) as string[];
      }
    }
  }

#if BRUNET_NUNIT
  /**
   * A mock class that is derived from RpcManager but instead of doing RPC calls, it
   * returns the values that are assigned by the testing classes.
   */
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
        _instance = new MockRpcManager(node.Rrm);
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
      Node n = new StructuredNode(new AHAddress(new RNGCryptoServiceProvider()));
      _rpc = XmlRpcManagerClient.GetXmlRpcManager("127.0.0.1", Port, "xm.rem", true);
      _mrm = MockRpcManager.GetInstance(n);
      _server = new XmlRpcManagerServer(Port);
      _server.Add(n, _mrm, "xm1.rem");
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
