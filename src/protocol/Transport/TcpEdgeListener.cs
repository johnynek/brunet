/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2005-2008  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System.Collections.Specialized;
using System.Threading;

using Brunet.Util;

namespace Brunet
{

  /**
   * A EdgeListener that uses TCP for the underlying
   * protocol.  This listener creates TCP edges.
   */

  public class TcpEdgeListener : EdgeListener, IEdgeSendHandler
  {
    protected readonly Socket _listen_sock;
    protected readonly IPEndPoint _local_endpoint;
    protected readonly object _send_sync;
    protected readonly Thread _loop;

    protected readonly IEnumerable _tas;

    protected readonly SingleReaderLockFreeQueue<SocketStateAction> ActionQueue;
   
    /**
     * This class holds all the mutable state about the sockets we are working
     * with.  It has no synchronization because it should only be modified
     * in select thread, and so there is no chance for thread-sync bugs.
     */
    protected class SocketState {
      /*
       * These variables can change BUT ONLY IN THE SELECT THREAD
       */
      public bool Run;
      public TAAuthorizer TAA;
      protected Hashtable _sock_to_rs;
      
      /*
       * These are all the fixed objects that never change
       */
      protected readonly ListDictionary _sock_to_constate;
      protected readonly ArrayList _con_socks;
      protected readonly object _send_sync;
      protected readonly ArrayList _socks_to_send;
      /*
       * This can be pretty big because it will only 
       * slow down processing the ActionQueue, sends are not
       * blocked by this.  No high performance items are
       * placed in the ActionQueue
       */
      protected readonly static int TIMEOUT_MS = 250;
      /*
       * This is the Public API of the state
       */
      public readonly ArrayList AllSockets;
      public readonly ArrayList ReadSocks;
      public readonly ArrayList ErrorSocks;
      public readonly ArrayList WriteSocks;
      public readonly Socket ListenSock;
      public readonly BufferAllocator BA;

      public SocketState(Socket listensock, object ss) {
        _sock_to_rs = new Hashtable();
        _sock_to_constate = new ListDictionary();
        _con_socks = new ArrayList();
        ListenSock = listensock;
        _send_sync = ss;
        _socks_to_send = new ArrayList();
        AllSockets = new ArrayList();
        ReadSocks = new ArrayList();
        ErrorSocks = new ArrayList();
        WriteSocks = new ArrayList();
        Run = true;
        /* Use a shared BufferAllocator for all Edges */
        BA = new BufferAllocator(2 + Packet.MaxLength);
        ListenSock.Listen(10);
      }
      public void AddEdge(TcpEdge e) {
        Socket s = e.Socket;
        AllSockets.Add(s); 
        ReceiveState rs = new ReceiveState(e, BA);
        _sock_to_rs[s] = rs;
      }
      public void AddSendWaiter(Socket s) {
        if( _socks_to_send.Contains(s) == false ) {
          _socks_to_send.Add(s);
        }
      }

      public void AddCreationState(Socket s, CreationState cs) {
        _sock_to_constate[ s ] = cs;
        _con_socks.Add(s);
      }

      public void CloseSocket(Socket s) {
        //We shouldn't be sending at the same time:
        lock( _send_sync ) {
          //We need to shutdown the socket:
          try {
            //Don't let more reading or writing:
            s.Shutdown(SocketShutdown.Both);
          }
          catch { }
          finally {
            //This can't throw an exception
            s.Close();
          }
        }
      }

      public void FlushSocket(Socket s) {
        //Let's try to flush the buffer:
        ReceiveState rs = (ReceiveState)_sock_to_rs[s];
        bool flushed = true;
        if( rs != null ) {
          lock( _send_sync ) {
            flushed = rs.Edge.Flush();
          }
        }
        if( flushed ) {
          //This socket is flushed, forget it:
          _socks_to_send.Remove(s);
        }
        else {
          //Make sure we continue to check this for writability
          //This is a "set-like" operation, Add... is idempotent
          AddSendWaiter(s);
        }
      }

      //Remove and return the CreationState
      public CreationState TakeCreationState(Socket s) {
        CreationState cs = (CreationState)_sock_to_constate[s];
        _sock_to_constate.Remove(s);
        _con_socks.Remove(s);
        return cs;
      }

      public TcpEdge GetEdge(Socket s) {
        ReceiveState rs = (ReceiveState)_sock_to_rs[s];
        if( rs != null ) {
          return rs.Edge;
        }
        return null;
      }

      public ReceiveState GetReceiveState(Socket s) {
        return (ReceiveState)_sock_to_rs[s];
      }

      public void RemoveEdge(TcpEdge e)
      {
        Socket s = e.Socket;
        // Go ahead and remove from the map. ... this needs to be done because
        // Socket's dynamic HashCode, a bug in an older version of Mono
        Hashtable new_s_to_rs = new Hashtable( _sock_to_rs.Count );
        foreach(DictionaryEntry de in _sock_to_rs) {
          ReceiveState trs = (ReceiveState)de.Value;
          if( e != trs.Edge ) {
            new_s_to_rs.Add( de.Key, trs );
          }
        }
        _sock_to_rs = new_s_to_rs;
        AllSockets.Remove(s); 
        _socks_to_send.Remove(s);
      }

      /**
       * Update the ReadSocks and ErrorSocks to see which sockets might be
       * ready for reading or need closing.
       */
      public void Select() {
        ReadSocks.Clear();
        ErrorSocks.Clear();
        WriteSocks.Clear();
        ReadSocks.AddRange(AllSockets);
        //Also listen for incoming connections:
        ReadSocks.Add(ListenSock);
        /*
         * POB: I cannot find any documentation on what, other than
         * out-of-band data, might be signaled with these.  As such
         * I am commenting them out.  If we don't see a reason to put
         * it back soon, please delete ErrorSocks from the code.
         * 11/19/2008
        ErrorSocks.AddRange(AllSockets);
        */
        //Here we do non-blocking connecting:
        WriteSocks.AddRange(_con_socks);
        //Here we add the sockets that are waiting to write:
        WriteSocks.AddRange(_socks_to_send);
        //An error signals that the connection failed
        ErrorSocks.AddRange(_con_socks);

        /*
         * Now we are ready to do our select and we only act on local
         * variables
         */
        try {
          //Socket.Select(ReadSocks, null, ErrorSocks, TIMEOUT_MS * 1000);
          Socket.Select(ReadSocks, WriteSocks, ErrorSocks, TIMEOUT_MS * 1000);
          //Socket.Select(ReadSocks, null, null, TIMEOUT_MS * 1000);
        }
        catch(System.ObjectDisposedException) {
            /*
             * This happens if one of the edges is closed while
             * a select call is in progress.  This is not weird,
             * just ignore it
             */
          ReadSocks.Clear();
          ErrorSocks.Clear();
          WriteSocks.Clear();
        }
        catch(Exception x) {
          //One of the Sockets gave us problems.  Perhaps
          //it was closed after we released the lock.
          Console.Error.WriteLine( x.ToString() );
          Thread.Sleep(TIMEOUT_MS);
        }
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

    protected int _is_started;
    public override bool IsStarted
    {
      get { return (1 == _is_started); }
    }

    override public TAAuthorizer TAAuth {
      /**
       * When we add a new TAAuthorizer, we have to check to see
       * if any of the old addresses are no good, in which case, we
       * close them
       */
      set {
        _ta_auth = value;
        ActionQueue.Enqueue( new CloseDeniedAction(this, value) );
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
      _is_started = 0;
      _listen_sock = new Socket(AddressFamily.InterNetwork,
                                SocketType.Stream, ProtocolType.Tcp);
      _listen_sock.LingerState = new LingerOption (true, 0);
      IPEndPoint tmp_ep = new IPEndPoint(IPAddress.Any, port);
      _listen_sock.Bind(tmp_ep);
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
      _send_sync = new object();
      _loop = new Thread( this.SelectLoop );
      //This is how we push jobs into the SelectThread
      ActionQueue = new SingleReaderLockFreeQueue<SocketStateAction>();
    }

    /* //////////////////////////
     * Here are all the Actions we take in the Select thread
     * This is what allows us to avoid locking, but also make sure things are
     * thread-safe.
     * /////////////////////////
     */

    /** Object subclass this is how we look at SocketState in the SelectThread;
     * do *NOT* keep a reference to SocketState after the Start() call.  You
     * should only access it in the Start() method, after that, let it go!
     */
    protected abstract class SocketStateAction {
      abstract public void Start(SocketState ss);
    }

    /** Class to handle closing in the select thread
     */
    protected class CloseAction : SocketStateAction {
      protected readonly TcpEdge _e;
      protected readonly SingleReaderLockFreeQueue<SocketStateAction> _queue;

      public CloseAction(TcpEdge e, SingleReaderLockFreeQueue<SocketStateAction> q) {
        _e = e;
        _queue = q;
      }

      public void CloseHandler(object edge, EventArgs ea) {
        _queue.Enqueue(this);
      }

      /** This will be called in select thread
       */
      public override void Start(SocketState ss) {
        ss.RemoveEdge(_e);
        ss.CloseSocket(_e.Socket);
      }
    }
    
    /** In the select thread, go through the sockets and close any now denied TAs
     */
    protected class CloseDeniedAction : SocketStateAction {
      public readonly TAAuthorizer TAA;
      public readonly TcpEdgeListener EL;
      public CloseDeniedAction(TcpEdgeListener el, TAAuthorizer taa) {
        TAA = taa;
        EL = el;
      }
      /*
       * Update the SocketState.TAA and check to see if any Edges need
       * to be closed.
       */
      public override void Start(SocketState ss) {
        ss.TAA = TAA;
        ArrayList bad_edges = new ArrayList();
        foreach(Socket s in ss.AllSockets) {
          TcpEdge e = ss.GetEdge(s);
          if( e != null ) {
            if( TAA.Authorize( e.RemoteTA ) == TAAuthorizer.Decision.Deny ) {
              //We can't close now, that would invalidate the AllSockets
              //iterator
              bad_edges.Add(e);
            }
          }
        }
        foreach(TcpEdge e in bad_edges) {
          EL.RequestClose(e);
          CloseAction ca = new CloseAction(e, null);
          ca.Start(ss);
        }
      }
    }
    
    /**
     * This object manages creating new outbound edges.
     */
    protected class CreationState : SocketStateAction {
      public readonly EdgeCreationCallback ECB;
      public readonly int Port;
      public readonly Queue IPAddressQueue;
      public readonly TcpEdgeListener TEL;
      public readonly WriteOnce<object> Result;


      public CreationState(EdgeCreationCallback ecb,
                           Queue ipq, int port,
                           TcpEdgeListener tel)
      {
        ECB = ecb;
        IPAddressQueue = ipq;
        Port = port;
        TEL = tel;
        Result = new WriteOnce<object>();
      }
      /**
       * Called when the socket is writable
       */
      public void HandleWritability(Socket s) {
        try {
          if( s.Connected ) {
            TcpEdgeListener.SetSocketOpts(s);
            TcpEdge e = new TcpEdge(TEL, false, s);
            Result.Value = e;
            //Handle closes in the select thread:
            CloseAction ca = new CloseAction(e, TEL.ActionQueue);
            e.CloseEvent += ca.CloseHandler;
            //Set the edge
            TEL.ActionQueue.Enqueue(this);
          }
          else {
            //This did not work out, close the socket and release the resources:
            HandleError(s);
          }
        }
        catch(Exception) {
          //This did not work out, close the socket and release the resources:
          //Console.WriteLine("Exception: {0}", x);
          HandleError(s);
        }
      }
       
      public void HandleError(Socket s) {
        s.Close();
        TEL.ActionQueue.Enqueue(this);
      }

      /**
       * Implements the SocketStateAction interface.  This is called to trigger the ECB
       * in the SelectThread so we don't have to worry about multiple threads
       * accessing variables.
       */
      public override void Start(SocketState ss) {
        object result = Result.Value;
        if( result != null ) {
          TcpEdge new_edge = result as TcpEdge;
          if( new_edge != null ) {
            //Tell the world about the new Edge:
            ss.AddEdge(new_edge);
            ECB(true, new_edge, null); 
          }
          else {
            ECB(false, null, (Exception)result); 
          }
        }
        else {
          //Try to make a new start:
          if( IPAddressQueue.Count <= 0 ) {
            ECB(false, null, new EdgeException("No more IP Addresses"));
          }
          else {
            Socket s = null;
            try {
              s = new Socket(AddressFamily.InterNetwork,
                                   SocketType.Stream,
                                   ProtocolType.Tcp);
              s.Blocking = false;
              ss.AddCreationState(s, this);
              IPAddress ipaddr = (IPAddress)IPAddressQueue.Dequeue();
              IPEndPoint end = new IPEndPoint(ipaddr, Port);
              IPAddress any = s.AddressFamily == AddressFamily.InterNetworkV6 
                              ? IPAddress.IPv6Any : IPAddress.Any;
              //This is a hack because of a bug in MS.Net and Mono:
              //https://bugzilla.novell.com/show_bug.cgi?id=349449
              //http://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=332142
              s.Bind(new IPEndPoint(any, 0));
              s.Connect(end);
            }
            catch(SocketException sx) {
              if( sx.SocketErrorCode != SocketError.WouldBlock ) {
                if( s != null ) {
                  ss.TakeCreationState(s);
                }
                ECB(false, null, new EdgeException(false, "Could not Connect", sx)); 
              }
              /* else Ignore the non-blocking socket error */
            }
          }
        }
      }
    }

    /*
     * Handle writing the to the log every interval
     */
    protected class LogAction : SocketStateAction {
      protected DateTime _last_debug;
      protected readonly TimeSpan _debug_period;
      protected readonly SingleReaderLockFreeQueue<SocketStateAction> _q;

      public LogAction(TimeSpan interval, SingleReaderLockFreeQueue<SocketStateAction> q) {
        _last_debug = DateTime.UtcNow;
        _debug_period = interval;
        _q = q;
      }

      public override void Start(SocketState ss) {
        DateTime now = DateTime.UtcNow;
        if (now - _last_debug > _debug_period) {
          _last_debug = now;
          ProtocolLog.Write(ProtocolLog.Monitor, String.Format("I am alive: {0}", now));
        }
        //Run ourselves again later.
        _q.Enqueue(this);
      }
    }

    /** Runs select on the sockets and reschedules itself.
     */
    protected class SelectAction : SocketStateAction {
      protected readonly TcpEdgeListener TEL;

      public SelectAction(TcpEdgeListener tel) {
        TEL = tel;
      }

      public override void Start(SocketState ss) {
        ss.Select();
        HandleWrites(ss);
        HandleErrors(ss);
        HandleReads(ss);
        //Enqueue ourselves to run again:
        TEL.ActionQueue.Enqueue(this);
      }
      protected void HandleErrors(SocketState ss) {
        ArrayList socks = ss.ErrorSocks;
        for(int i = 0; i < socks.Count; i++) {
          Socket s = (Socket)socks[i];
          CreationState cs = ss.TakeCreationState( s );
          if( cs != null ) {
            cs.HandleError(s);
          }
        }
      }
      protected void HandleWrites(SocketState ss) {
        ArrayList socks = ss.WriteSocks;
        for(int i = 0; i < socks.Count; i++) {
          Socket s = (Socket)socks[i];
          CreationState cs = ss.TakeCreationState( s );
          if( cs != null ) {
            cs.HandleWritability(s);
          }
          else {
            //Let's try to flush the buffer:
            try {
              ss.FlushSocket(s);
            }
            catch {
              /*
               * We should close this edge
               */
              TcpEdge tcpe = ss.GetEdge(s);
              TEL.RequestClose(tcpe);
              //Go ahead and forget about this socket.
              CloseAction ca = new CloseAction(tcpe, null);
              ca.Start(ss);
            }
          }
        }
      }
      protected void HandleReads(SocketState ss) {
        ArrayList readsocks = ss.ReadSocks;
        Socket listen_sock = ss.ListenSock;
        for(int i = 0; i < readsocks.Count; i++) {
          Socket s = (Socket)readsocks[i];
          //See if this is a new socket
          if( s == listen_sock ) {
            TcpEdge e = null;
            Socket new_s = null;
            try {
              new_s = listen_sock.Accept();
              IPEndPoint rep = (IPEndPoint)new_s.RemoteEndPoint;
              new_s.LingerState = new LingerOption (true, 0);
              TransportAddress rta =
                       TransportAddressFactory.CreateInstance(TransportAddress.TAType.Tcp, rep);
              if( ss.TAA.Authorize(rta) == TAAuthorizer.Decision.Deny ) {
                //No thank you Dr. Evil
                Console.Error.WriteLine("Denying: {0}", rta);
                new_s.Close();
              }
              else {
                //This edge looks clean
                TcpEdgeListener.SetSocketOpts(s);
                e = new TcpEdge(TEL, true, new_s);
                ss.AddEdge(e);
                //Handle closes in the select thread:
                CloseAction ca = new CloseAction(e, TEL.ActionQueue);
                e.CloseEvent += ca.CloseHandler;
                TEL.SendEdgeEvent(e);
              }
            }
            catch(Exception) {
              //Looks like this Accept has failed.  Do nothing
              //Console.Error.WriteLine("New incoming edge ({0}) failed: {1}", new_s, sx);
              //Make sure the edge is closed
              if( e != null ) {
                TEL.RequestClose(e);
                //Go ahead and forget about this socket.
                CloseAction ca = new CloseAction(e, null);
                ca.Start(ss);
              }
              else if( new_s != null) {
                //This should not be able to throw an exception:
                new_s.Close();
              }
            }
          }
          else {
            ReceiveState rs = ss.GetReceiveState(s);
            if( rs != null && !rs.Receive() ) {
              TEL.RequestClose(rs.Edge);
              //Go ahead and forget about this socket.
              CloseAction ca = new CloseAction(rs.Edge, null);
              ca.Start(ss);
            }
          }
        }
      }
    
    }
    
    /** Set the SocketState.Run to false, this stops the Select thread
     */
    protected class StopAction : SocketStateAction {
      public override void Start(SocketState ss) { ss.Run = false; }
    }

    /** Used to signal that there is a socket waiting to write
     */
    protected class SendWaitAction : SocketStateAction {
      public readonly Socket Socket;
      public SendWaitAction(Socket s) { Socket = s; }
      public override void Start(SocketState ss) { ss.AddSendWaiter(Socket); }
    }

    /** This Action is excuted AFTER the StopAction, kind of a "Destructor" 
     */
    protected class ShutdownAction : SocketStateAction {
      protected readonly TcpEdgeListener TEL;
      public ShutdownAction(TcpEdgeListener tel) {
        TEL = tel;
      }
      public override void Start(SocketState ss) {
        /*
         * Note, we are the only thread running actions from the queue,
         * so, no change to ss can happen concurrently with this logic
         */
        ArrayList close_actions = new ArrayList();
        foreach(Socket s in ss.AllSockets) {
          TcpEdge e = ss.GetEdge(s);
          TEL.RequestClose(e);
          /*
           * We can't just call ca.Start(ss) because that
           * would change ss.AllSockets
           */
          close_actions.Add(new CloseAction(e, null));
        }
        foreach(CloseAction ca in close_actions) {
          ca.Start(ss);
        }
        //Close the main socket:
        ss.ListenSock.Close();
      }
    }


    /* //////////////////////////////
     *
     * Here are all the normal methods of TcpEdgeListener
     *
     * //////////////////////////////
     */


    public override void CreateEdgeTo(TransportAddress ta, EdgeCreationCallback ecb)
    {
      try {
        if( !IsStarted ) {
          throw new EdgeException("TcpEdgeListener is not started");
        }
        else if( ta.TransportAddressType != TransportAddress.TAType.Tcp ) {
	        throw new EdgeException(ta.TransportAddressType.ToString()
				    + " is not my type: Tcp");
        }
        else if( _ta_auth.Authorize(ta) == TAAuthorizer.Decision.Deny ) {
            //Too bad.  Can't make this edge:
	        throw new EdgeException( ta.ToString() + " is not authorized");
        }
        else {
          //Everything looks good:
	        ArrayList tmp_ips = new ArrayList();
	        tmp_ips.Add(((IPTransportAddress)ta).GetIPAddress());
          CreationState cs = new CreationState(ecb,
                                           new Queue( tmp_ips ),
                                           ((IPTransportAddress) ta).Port,
                                           this);
          ActionQueue.Enqueue(cs);
        }
      } catch(Exception e) {
	      ecb(false, null, e);
      }
    }

    ///Set the standard options for this socket
    public static void SetSocketOpts(Socket s) {
      s.Blocking = false; //Make sure the socket is not blocking
      //s.NoDelay = true; //Disable Nagle
      /*
       * I'm not sure this is helping at all, but we are doubling
       * the usual buffer size in the hopes that it will reduce
       * problems of lost packets.
       */
      s.SendBufferSize = 16384;
      s.ReceiveBufferSize = 16384;
    }

    public override void Start()
    {
      if( 1 == Interlocked.Exchange(ref _is_started, 1) ) {
        //We are calling start again, that is not good.
        throw new Exception("Cannot start more than once");
      }
      _loop.Start();
    }

    public override void Stop()
    {
      ActionQueue.Enqueue(new StopAction());

      //Don't join on the current thread, that would block forever
      if(Thread.CurrentThread != _loop ) {
        _loop.Join();
      }
      base.Stop();
    }
    

    /* ***************************************************** */
    /* Protected Methods */
    /* ***************************************************** */


    protected void SelectLoop()
    {
      Thread.CurrentThread.Name = "tcp_select_thread";

      //No one can see this except this thread, so there is no
      //need for thread synchronization
      SocketState ss = new SocketState(_listen_sock, _send_sync);
      ss.TAA = _ta_auth;

      if( ProtocolLog.Monitor.Enabled ) {
        // log every 5 seconds.
        ActionQueue.Enqueue(new LogAction(new TimeSpan(0,0,0,0,5000), ActionQueue));
      }
      //Start the select action:
      ActionQueue.Enqueue(new SelectAction(this)); 
      bool got_action = false;
      while(ss.Run) {
        SocketStateAction a = ActionQueue.TryDequeue(out got_action);
        if( got_action ) { a.Start(ss); }
      }
      ShutdownAction sda = new ShutdownAction(this);
      sda.Start(ss);
      //Empty the queue to remove references to old objects
      do {
        ActionQueue.TryDequeue(out got_action);
      } while(got_action);
    }

    /**
     * Represents the state of the packet receiving 
     */
    protected class ReceiveState {
      public byte[] Buffer;
      public int Offset;
      public int Length;

      public int CurrentOffset;
      public int RemainingLength;

      public bool ReadingSize;

      public readonly TcpEdge Edge;
      protected readonly Socket _s;      
      protected readonly BufferAllocator _ba;

      public ReceiveState(TcpEdge e, BufferAllocator ba) {
        Edge = e;
        _s = e.Socket;
        _ba = ba;
      }

      protected void Reset(byte[] buf, int off, int length, bool readingsize) {
        Buffer = buf;
        Offset = off;
        Length = length;
        CurrentOffset = off;
        RemainingLength = length;
        ReadingSize = readingsize;
      }

      /**
       * Do as much of a read as we can
       * @return true if the socket is still ready, false if we should close
       * the edge.
       */
      public bool Receive() {
        int got = 0;
        int avail = 0;
        do {
          if( Buffer == null ) {
            //Time to read the size:
            Reset(_ba.Buffer, _ba.Offset, 2, true);
            _ba.AdvanceBuffer(2);
          }
          try {
            got = _s.Receive(Buffer, CurrentOffset, RemainingLength, SocketFlags.None);
            avail = _s.Available;
          }
          catch(SocketException) {
            //Some OS error, just close the edge:
            Buffer = null;
            return false;
          }
          if( got == 0 ) {
            //this means the edge is closed:
            return false;
          }
          CurrentOffset += got;
          RemainingLength -= got;
          if(RemainingLength == 0) {
            //Time to do some action:
            if( ReadingSize ) {
              short size = NumberSerializer.ReadShort(Buffer, Offset);
              if( size <= 0 ) {
                //This doesn't make sense, later we might use this to code
                //something else
                return false;
              }
              //Start to read the packet:
              Reset(_ba.Buffer, _ba.Offset, size, false);
              _ba.AdvanceBuffer(size);
            }
            else {
              try {
                Edge.ReceivedPacketEvent(MemBlock.Reference(Buffer, Offset, Length));
              }
              catch(EdgeClosedException) {
                return false;
              }
              finally {
                //Drop the reference and signal we are ready to read the next
                //size
                Buffer = null;
              }
            }
          }
        } while( avail > 0 );
        return true;
      }
    }



    public void HandleEdgeSend(Edge from, ICopyable p) {
      TcpEdge sender = (TcpEdge) from;
      try {
        bool flushed = true;
        lock(_send_sync) {
          //Try to fill up the buffer:
          sender.WriteToBuffer(p);
          //Okay, we loaded the whole packet into the TcpEdge's buffer
          //now it is time to try to flush the buffer:
          flushed = sender.Flush();  
        }
        if( !flushed ) {
          /*
           * We should remember to try again when the socket is
           * writable
           */
          ActionQueue.Enqueue(new SendWaitAction(sender.Socket));
        }
      }
      catch(EdgeException ex) {
        if( false == ex.IsTransient ) {
          //Go ahead and forget about this socket.
          RequestClose(from); 
          ActionQueue.Enqueue(new CloseAction(sender, null));
        }
        //Rethrow the exception
        throw;
      }
      catch(Exception x) {
        //Assume any other error is transient:
        throw new EdgeException(true, String.Format("Could not send on: {0}", from), x);
      }
    }
  }

}
