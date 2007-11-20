/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2005-2007  P. Oscar Boykin <boykin@pobox.com>, University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

//#define POB_DEBUG

using System;
using System.Net.Sockets;
using System.Net;
using System.Collections;
using System.Threading;

namespace Brunet
{

  /**
   * A EdgeListener that uses TCP for the underlying
   * protocol.  This listener creates TCP edges.
   */

  public class TcpEdgeListener : EdgeListener
  {
    protected readonly Socket _listen_sock;
    protected IPEndPoint _local_endpoint;
    protected readonly object _sync;
    protected readonly Thread _loop;
    protected volatile bool _send_edge_events;
    volatile protected bool _run;

    protected ArrayList _all_sockets;
    protected ArrayList _send_sockets;
    volatile protected Hashtable _sock_to_edge;
    protected IEnumerable _tas;
    /**
     * This inner class holds the connection state information
     */
    protected class CreationState {
      public EdgeCreationCallback ECB;
      public int Port;
      public Queue IPAddressQueue;
      public Socket Socket;

      public CreationState(EdgeCreationCallback ecb,
                           Queue ipq, int port)
      {
        ECB = ecb;
        IPAddressQueue = ipq;
        Port = port;
      }

    }

    public override IEnumerable LocalTAs
    {
      get { return  _tas; }
    }

    public override TransportAddress.TAType TAType
    {
      get
      {
        return TransportAddress.TAType.Tcp;
      }
    }

    protected bool _is_started;
    public override bool IsStarted
    {
      get { lock( _sync ) { return _is_started; } }
    }

    override public TAAuthorizer TAAuth {
      /**
       * When we add a new TAAuthorizer, we have to check to see
       * if any of the old addresses are no good, in which case, we
       * close them
       */
      set {
        ArrayList bad_edges = new ArrayList();
        lock( _sync ) {
          _ta_auth = value;
          IDictionaryEnumerator en = _sock_to_edge.GetEnumerator();
          while( en.MoveNext() ) {
            Edge e = (Edge)en.Value;
            if( _ta_auth.Authorize( e.RemoteTA ) == TAAuthorizer.Decision.Deny ) {
              bad_edges.Add(e);
            }
          }
        }
        //Close the newly bad Edges.
        foreach(Edge e in bad_edges) {
          e.Close();   
        }
      }
    }    

    public TcpEdgeListener(): this(0, null, null)
    {
    }
    /**
     * @param port the port to listen on
     * This tries to guess the local IP address as best it can
     * and allows connections from any remote node
     */
    public TcpEdgeListener(int port):this(port, null,null) { }
    /**
     * @param port the port to listen on
     * @param ipList an IEnumerable object of IPAddress objects.
     * This allows connections from any remote peer (TAAuthorizer always
     * allows)
     */
    public TcpEdgeListener(int port, IEnumerable ipList) : this(port, ipList, null) { }
    /**
     * @param port the port to listen on
     * @param ipList an IEnumerable object of IPAddress objects.
     * @param ta_auth the TAAuthorizer to use for remote nodes
     */
    public TcpEdgeListener(int port, IEnumerable local_config_ips, TAAuthorizer ta_auth)
    {
      _is_started = false;
      _listen_sock = new Socket(AddressFamily.InterNetwork,
                                SocketType.Stream, ProtocolType.Tcp);
      _listen_sock.LingerState = new LingerOption (true, 0);
      _local_endpoint = new IPEndPoint(IPAddress.Any, port);
      _listen_sock.Bind(_local_endpoint);
      _local_endpoint = (IPEndPoint) _listen_sock.LocalEndPoint;
      port = _local_endpoint.Port;

      /**
       * We get all the IPAddresses for this computer
       */
      if( local_config_ips == null ) {
        _tas = TransportAddressFactory.CreateForLocalHost(TransportAddress.TAType.Tcp, port);
      }
      else {
        _tas = TransportAddressFactory.Create(TransportAddress.TAType.Tcp, port, local_config_ips);
      }

      _ta_auth = ta_auth;
      if( _ta_auth == null ) {
        //Always authorize in this case:
        _ta_auth = new ConstantAuthorizer(TAAuthorizer.Decision.Allow);
      }
      _sync = new Object();
      _send_edge_events = false;
      _run = true;
      _all_sockets = new ArrayList();
      _send_sockets = new ArrayList();
      _sock_to_edge = new Hashtable();
      _loop = new Thread( new ThreadStart( this.SelectLoop ) );
    }

    /**
     */
    public override void CreateEdgeTo(TransportAddress ta, EdgeCreationCallback ecb)
    {
      try {
      if( !IsStarted )
      {
	throw new EdgeException("TcpEdgeListener is not started");
      }
      else if( ta.TransportAddressType != this.TAType ) {
	throw new EdgeException(ta.TransportAddressType.ToString()
				+ " is not my type: " + this.TAType.ToString());
      }
      else if( _ta_auth.Authorize(ta) == TAAuthorizer.Decision.Deny ) {
        //Too bad.  Can't make this edge:
	throw new EdgeException( ta.ToString() + " is not authorized");
      }
      else {
        //Everything looks good:
        CreationState cs = new CreationState(ecb,
                                           new Queue( ((IPTransportAddress)ta).GetIPAddresses() ),
                                           ((IPTransportAddress) ta).Port);
        TryNextIP( cs );
      }
      } catch(Exception e) {
	ecb(false, null, e);
      }
    }

    public override void Start()
    {
      lock( _sync ) {
        if( _is_started ) {
          //We are calling start again, that is not good.
          throw new Exception("Cannot start more than once");
        }
        _listen_sock.Listen(10);
        _is_started = true;
        _send_edge_events = true;
        _run = true;
        _loop.Start();
      }
    }

    public override void Stop()
    {
      //This should stop the thread gracefully
      _run = false;
      if(Thread.CurrentThread != _loop ) {
        //Don't join on the current thread, that would block forever
        _loop.Join();
      }
    }

    /* ***************************************************** */
    /* Protected Methods */
    /* ***************************************************** */

    /**
     * Called when BeginConnect returns
     */
    protected void ConnectCallback(IAsyncResult ar)
    {
      CreationState cs = null;
      Socket s = null;
      TcpEdge e;
      bool success;
      try {
        cs = (CreationState)ar.AsyncState;
        s = cs.Socket;
        bool start_edge = false;
        s.EndConnect(ar);
        start_edge = s.Connected;
        if( start_edge ) {
          e = new TcpEdge(s, false, this);
          AddEdge(e);
#if PLAB_LOG
          e.Logger = this.Logger;
#endif
          //Start listening for incoming packets:
          success = true;
        }
        else {
          //This did not work out, close the socket and release the resources:
          s.Close();
          e = null;
          success = false;
        }
        cs.ECB(success, e, null);
      }
      catch(Exception) {
        //This did not work out, close the socket and release the resources:
	//Console.Error.WriteLine( x );
        if( s != null) { s.Close(); }
        if( cs != null ) {
          /*
	   * If the connection fails and we can't look up the connection state,
	   * we must stop.  So, only TryNextIP if we can look up the connection
	   * state.
	   *
	   * On shutdown, we may get a:
	   * System.ObjectDisposedException: The object was used after being disposed.
           * in <0x000ba> System.Net.Sockets.Socket:EndConnect (IAsyncResult result)
	   */
	  TryNextIP( cs );
	}
      }
    }

    protected void TryNextIP(CreationState cs)
    {
      if( cs.IPAddressQueue.Count <= 0 ) {
        cs.ECB(false, null, new EdgeException("No more IP Addresses") );
      }
      else {
        Socket s = null;
        try {
          IPAddress ipaddr = (IPAddress)cs.IPAddressQueue.Dequeue();
          IPEndPoint end = new IPEndPoint(ipaddr, cs.Port);
          s = new Socket(end.AddressFamily,
                         SocketType.Stream, ProtocolType.Tcp);
          /**
           * @throw ArgumentNullException for connect if end is null.
           * @throw InvalidOperationException for connect 
           * if an asynchronous call is pending and a blocking method 
           * has been called.
           * @throw ObjectDisposedException for the Connectif the current
           * instance has been disposed 
           * @throw System.Security.SecurityException if a caller in the call 
           * stack does not have the required permission.
           * @throw System.Net.Sockets.SocketException.
           */
          s.SetSocketOption(SocketOptionLevel.Tcp,
                            SocketOptionName.NoDelay,
                            1);
          /* Store the callback in the socket table temporarily: */
          cs.Socket = s;
          s.BeginConnect(end, this.ConnectCallback, cs);
        }
        catch(Exception x) {
          cs.ECB(false, null, x);
        }
      }
    }

    protected void SelectLoop()
    {
      int timeout_ms = 10; //it was 10 changed to 1 by kl
      ArrayList readsocks = new ArrayList();
      ArrayList writesocks = new ArrayList();
      ArrayList errorsocks = new ArrayList();
      //These hold refences to the above OR null when they are empty
      ArrayList rs;
      ArrayList es;
      ArrayList ws;
      
      /* Use a shared BufferAllocator for all Edges */
      BufferAllocator buf = new BufferAllocator(2 + Packet.MaxLength);
     
      while(_run)
      {
        readsocks.Clear();
        writesocks.Clear();
        errorsocks.Clear();
        /*
         * Set up readsocks, errorsocks, and writesocks
         */
        //Try to get the _all_sockets:
        ArrayList all_s = Interlocked.Exchange(ref _all_sockets, null);
        if( all_s != null ) {
          //We got the all sockets list:
          readsocks.AddRange( all_s );
          errorsocks.AddRange( all_s );
          //Put it back:
          Interlocked.Exchange(ref _all_sockets, all_s);
        }
        //Try to get the _send_sockets:
        ArrayList send_s = Interlocked.Exchange(ref _send_sockets, null);
        if( send_s != null ) {
          //We got the send sockets:
          writesocks.AddRange( send_s );
          if( all_s == null ) {
            //We didn't get the list of all sockets, but go ahead and put
            //the write sockets into the error list:
            errorsocks.AddRange( send_s );
          }
          //Put the _send_sockets back:
          Interlocked.Exchange(ref _send_sockets, send_s);
        }
        if( _send_edge_events ) {
          //Also listen for incoming connections:
          readsocks.Add(_listen_sock);
          errorsocks.Add(_listen_sock);
        }
        /*
         * Now we are ready to do our select and we only act on local
         * variables
         */
        //This is an optimization to reduce memory allocations in Select
        rs = (readsocks.Count > 0) ? readsocks : null;
        es = (errorsocks.Count > 0) ? errorsocks : null;
        ws = (writesocks.Count == 0) ? null : writesocks;
        if( rs != null || es != null || ws != null ) {
          //There are some socket operations to do
#if POB_DEBUG
          Console.Error.WriteLine("Selecting");
          DateTime now = DateTime.UtcNow;
#endif
          try {
            Socket.Select(rs, ws, es, timeout_ms * 1000);
#if POB_DEBUG
            Console.Error.WriteLine("Selected for: {0}", DateTime.UtcNow - now);
#endif
          }
          catch(System.ObjectDisposedException) {
            /*
             * This happens if one of the edges is closed while
             * a select call is in progress.  This is not weird,
             * just ignore it
             */
            rs = null;
            es = null;
            ws = null;
          }
          catch(Exception x) {
            //One of the Sockets gave us problems.  Perhaps
            //it was closed after we released the lock.
#if POB_DEBUG
            Console.Error.WriteLine( x.ToString() );
#endif
            Console.Error.WriteLine( x.ToString() );
            Thread.Sleep(timeout_ms);
          }
        }
        else {
          //Wait 1 ms and try again
#if POB_DEBUG
          Console.Error.WriteLine("Waiting");
#endif
          Thread.Sleep(1);
        }
        if( es != null ) {
          HandleErrors(es);
        }
        if( rs != null ) {
          HandleReads(rs, buf);
        }
        if( ws != null ) {
          HandleWrites(ws, buf);
        }
      }//End of while loop
      /*
       * We are done, so close all the sockets
       */
      ArrayList tmp = null;
      do {
        tmp = Interlocked.Exchange(ref _all_sockets, tmp);
      } while( tmp == null );
      ArrayList copy = new ArrayList(tmp);
      Interlocked.Exchange(ref _all_sockets, tmp);
      
      foreach(Socket s in copy) {
        Edge e = (Edge)_sock_to_edge[s];
        if( e != null ) {
          e.Close();
        }
        s.Close();
      }
      //Close the main socket:
      _listen_sock.Close();
    }

    protected void AddEdge(TcpEdge e) {
      Socket s = e.Socket;

      ArrayList all_s = null;
      //Acquire _all_sockets
      do {
        all_s = Interlocked.Exchange(ref _all_sockets, all_s);
      } while( all_s == null );
      all_s.Add(s);
      //Put it back:
      Interlocked.Exchange(ref _all_sockets, all_s);

      lock( _sync ) {
        //lock before we change the Hashtable
        _sock_to_edge[s] = e;
      }
      try {
        e.CloseEvent += this.CloseHandler;
      }
      catch { CloseHandler(e, null); }
    }

    protected void CloseHandler(object edge, EventArgs arg)
    {
      TcpEdge e = (TcpEdge)edge;
      Socket s = e.Socket;
      // Go ahead and remove from the map. ... this needs to be done because
      // Socket's dynamic HashCode
      lock( _sync ) {
        Hashtable new_s_to_e = new Hashtable( _sock_to_edge.Count );
        foreach(DictionaryEntry de in _sock_to_edge) {
          if( e != de.Value ) {
            new_s_to_e.Add( de.Key, de.Value );
          }
        }
        _sock_to_edge = new_s_to_e;
      }

      ArrayList all_s = null;
      //Acquire _all_sockets
      do {
        all_s = Interlocked.Exchange(ref _all_sockets, all_s);
      } while( all_s == null );

      ArrayList send_s = null;
      //Acquire _send_sockets
      do {
        send_s = Interlocked.Exchange(ref _send_sockets, send_s);
      } while( send_s == null );
      //Remove from both:
      all_s.Remove(s);
      send_s.Remove(s);
      /*
       * Put them both back
       */
      Interlocked.Exchange(ref _all_sockets, all_s);
      Interlocked.Exchange(ref _send_sockets, send_s);
    }

    protected void HandleErrors(ArrayList errorsocks) {
      for(int i = 0; i < errorsocks.Count; i++) {
        Edge e = (Edge)_sock_to_edge[ errorsocks[i] ];
        if( e != null ) { e.Close(); }
#if POB_DEBUG
        Console.Error.WriteLine("TcpEdgeListener closing: {0}", e);
#endif
      }
    }
    protected void HandleReads(ArrayList readsocks, BufferAllocator buf) {
      for(int i = 0; i < readsocks.Count; i++) {
        object s = readsocks[i];
        //See if this is a new socket
        if( s == _listen_sock ) {
          TcpEdge e = null;
          try {
            Socket new_s = _listen_sock.Accept();
            new_s.LingerState = new LingerOption (true, 0);
            TransportAddress rta = TransportAddressFactory.CreateInstance(this.TAType,
                                    (IPEndPoint)new_s.RemoteEndPoint);
            if( _ta_auth.Authorize(rta) == TAAuthorizer.Decision.Deny ) {
              //No thank you Dr. Evil
              Console.Error.WriteLine("Denying: {0}", rta);
              new_s.Close();
            }
            else {
              //This edge looks clean
              e = new TcpEdge(new_s, true, this);
              AddEdge(e);
  #if POB_DEBUG
              Console.Error.WriteLine("New Edge: {0}", e);
  #endif
              SendEdgeEvent(e);
            }
          }
          catch(Exception sx) {
          //Looks like this Accept has failed.  Do nothing
            Console.Error.WriteLine("New incoming edge ({1}) failed: {0}", sx, e);
            //Make sure the edge is closed
            if( e != null ) { e.Close(); }
          }
        }
        else {
          TcpEdge e = (TcpEdge)_sock_to_edge[s];
#if POB_DEBUG
          Console.Error.WriteLine("DoReceive: {0}", e);
#endif
          //It is really important not to lock across this function call
          if( e != null ) { e.DoReceive(buf); }
          else {
            //Console.Error.WriteLine("ERROR: Receive Socket: {0} not associated with an edge", s);
          }
        }
      }
    }

    protected void HandleWrites(ArrayList writesocks, BufferAllocator b) {
      for(int i = 0; i < writesocks.Count; i++) {
        object s = writesocks[i];
        TcpEdge e = (TcpEdge)_sock_to_edge[s];
#if POB_DEBUG
          Console.Error.WriteLine("DoSend: {0}", e);
#endif
        if( e != null ) { e.DoSend(b); }
        else {
          //Console.Error.WriteLine("ERROR: Send Socket: {0} not associated with an edge", s);
        }
      }
    }

    /**
     * TcpEdge objects call this method when their
     * send state changes (from true to false or vice-versa).
     */
    public void SendStateChange(TcpEdge e, bool need_to_send)
    {
      ArrayList send_s = null;
      do {
        send_s = Interlocked.Exchange(ref _send_sockets, send_s);
      } while( send_s == null );

      try {
        if( need_to_send && !e.IsClosed ) {
          send_s.Add(e.Socket);
        }
        else {
          send_s.Remove(e.Socket);
        }
      }
      finally {
        Interlocked.Exchange(ref _send_sockets, send_s);
      }
    }

  }

}
