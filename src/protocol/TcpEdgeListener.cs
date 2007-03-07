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

//This flag must only be defined when the same flag is defined
//in TcpEdge
#define TCP_SELECT
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
    protected Socket _listen_sock;
    protected IPEndPoint _local_endpoint;
    protected object _sync;
    protected Thread _loop;
    protected bool _send_edge_events;
    protected bool _run;

    protected ArrayList _all_sockets;
    protected ArrayList _send_sockets;
    protected Hashtable _sock_to_edge;
    protected IEnumerable _tas;
    /**
     * This inner class holds the connection state information
     */
    protected class CreationState {
      public EdgeCreationCallback ECB;
      public int Port;
      public Queue IPAddressQueue;

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

    public TcpEdgeListener(int port):this(port, null)
    {
    }
    public TcpEdgeListener(int port, IPAddress[] ipList)
           : this(port, ipList, null)
    {
    }
    public TcpEdgeListener(int port, IEnumerable local_config_ips, TAAuthorizer ta_auth)
    {
      _is_started = false;
      
      /**
       * We get all the IPAddresses for this computer
       */
      if( local_config_ips == null ) {
        _tas = TransportAddressFactory.CreateForLocalHost(TransportAddress.TAType.Tcp, port);
      }
      else {
        _tas = TransportAddressFactory.Create(TransportAddress.TAType.Tcp, port, local_config_ips);
      }
      //_tas = GetIPTAs(TransportAddress.TAType.Tcp, port, ipList);

      _local_endpoint = new IPEndPoint(IPAddress.Any, port);
      _listen_sock = new Socket(AddressFamily.InterNetwork,
                                SocketType.Stream, ProtocolType.Tcp);
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
    }

    /**
     */
    public override void CreateEdgeTo(TransportAddress ta, EdgeCreationCallback ecb)
    {
      if( !IsStarted )
      {
        ecb(false, null,
            new EdgeException("TcpEdgeListener is not started") );
      }
      else if( ta.TransportAddressType != this.TAType ) {
        ecb(false, null,
            new EdgeException(ta.TransportAddressType.ToString()
                              + " is not my type: " + this.TAType.ToString() ) );
      }
      else if( _ta_auth.Authorize(ta) == TAAuthorizer.Decision.Deny ) {
        //Too bad.  Can't make this edge:
        ecb(false, null,
            new EdgeException( ta.ToString() + " is not authorized") );
      }
      else {
        //Everything looks good:
        CreationState cs = new CreationState(ecb,
                                           new Queue( ((IPTransportAddress)ta).GetIPAddresses() ),
                                           ((IPTransportAddress) ta).Port);
        TryNextIP( cs );
      }
    }

    public override void Start()
    {
      lock( _sync ) {
       if( _is_started ) {
         //We are calling start again, that is not good.
         throw new Exception("Cannot start more than once");
       }
        _is_started = true;
        _listen_sock.Bind(_local_endpoint);
        _listen_sock.Listen(10);
      }

      _send_edge_events = true;
      _loop = new Thread( new ThreadStart( this.SelectLoop ) );
      _run = true;
      _loop.Start();
    }

    public override void Stop()
    {
      //This should stop the thread gracefully
      _run = false;
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
      try {
        s = (Socket)ar.AsyncState;
        lock( _sync ) {
          cs = (CreationState)_sock_to_edge[s];
          _sock_to_edge.Remove(s);
        }

        s.EndConnect(ar);
        if (s.Connected) {
          //This new edge is NOT an incoming edge, thus the "false"
          TcpEdge e = new TcpEdge(s, false, this);
#if PLAB_LOG
          e.Logger = this.Logger;
#endif
          lock( _sync ) {
            _all_sockets.Add(s);
            _sock_to_edge[s] = e;
          }
          e.CloseEvent += new EventHandler(this.CloseHandler);
          //Start listening for incoming packets:
          e.Start();
          //We have success:
          cs.ECB(true, e, null);
        }
        else {
          //This did not work out, close the socket and release the resources:
          s.Close();
        }
      }
      catch(Exception) {
        //This did not work out, close the socket and release the resources:
	//System.Console.Error.WriteLine( x );
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
          lock(_sync) {
            /* Store the callback in the socket table temporarily: */
            if( s == null ) {
              Console.WriteLine("Null socket");
	    }
            _sock_to_edge[s] = cs;
          }
          s.BeginConnect(end, new AsyncCallback(this.ConnectCallback), s);
        }
        catch(Exception x) {
          lock( _sync ) {
            if( s != null ) {
              _sock_to_edge.Remove(s);
	    }
          }
          cs.ECB(false, null, x);
        }
      }
    }

    protected void SelectLoop()
    {
      int timeout_ms = 10; //it was 10 changed to 1 by kl
      ArrayList readsocks = null;
      ArrayList writesocks = null;
      ArrayList errorsocks = null;
      while(_run)
      {
        lock( _sync ) {
          if( _all_sockets.Count > 0 ) {
            readsocks = new ArrayList( _all_sockets );
            errorsocks = new ArrayList( _all_sockets );
          }
          else {
            readsocks = null;
            errorsocks = null;
          }
          if( _send_sockets.Count > 0 ) {
            writesocks = new ArrayList( _send_sockets );
          }
          else {
            writesocks = null;
          }
        }
        if( _send_edge_events ) {
          //Also listen for incoming connections:
          if( readsocks == null ) {
            readsocks = new ArrayList();
          }
          if( errorsocks == null ) {
            errorsocks = new ArrayList();
          }
          readsocks.Add(_listen_sock);
          errorsocks.Add(_listen_sock);
        }
        if( readsocks != null ||
            errorsocks != null ||
            writesocks != null ) {
          //There are some socket operations to do
#if POB_DEBUG
          Console.WriteLine("Selecting");
#endif
          try {
            Socket.Select(readsocks,
                          writesocks,
                          errorsocks,
                          timeout_ms * 1000);
          }
          catch(Exception) {
            //One of the Sockets gave us problems.  Perhaps
            //it was closed after we released the lock.
#if POB_DEBUG
            Console.Error.WriteLine( x.ToString() );
#endif
            Thread.Sleep(timeout_ms);
          }
        }
        else {
          //Wait 10ms and try again
#if POB_DEBUG
          Console.WriteLine("Waiting");
#endif
          Thread.Sleep(timeout_ms);
        }

        if( errorsocks != null ) {
          foreach(Socket s in errorsocks)
          {
            if( s == _listen_sock ) {

            }
            else {
              TcpEdge e = null;
              lock( _sync ) {
                if( s != null && _sock_to_edge.Contains(s) ) {
                  e = (TcpEdge)_sock_to_edge[s];
                }
              }
              //It is really important not to lock across this function call
              if( e != null ) { e.Close(); }
#if POB_DEBUG
              Console.Error.WriteLine("TcpEdgeListener closing: {0}", this);
#endif
            }
          }
        }//End of errorsocks
        if( readsocks != null ) {
          foreach(Socket s in readsocks)
          {
            //See if this is a new socket
            if( s == _listen_sock ) {
	      try {
                Socket new_s = s.Accept();
                TransportAddress rta = TransportAddressFactory.CreateInstance(this.TAType,
                                        (IPEndPoint)new_s.RemoteEndPoint);
                if( _ta_auth.Authorize(rta)
                    == TAAuthorizer.Decision.Deny ) {
                  //No thank you Dr. Evil
                  Console.Error.WriteLine("Denying: {0}", rta);
                  new_s.Close();
                }
                else {
                  //This edge looks clean
                  TcpEdge e = new TcpEdge(new_s, true, this);
#if PLAB_LOG
                  e.Logger = this.Logger;
#endif
                  lock( _sync ) {
                    _all_sockets.Add(new_s);
                    _sock_to_edge[new_s] = e;
                  }
                  e.CloseEvent += new EventHandler(this.CloseHandler);
#if POB_DEBUG
                  Console.Error.WriteLine("New Edge: {0}", e);
#endif
                  SendEdgeEvent(e);
                  e.Start();
                }
	      }
	      catch(SocketException sx) {
                //Looks like this Accept has failed.  Do nothing
                Console.Error.WriteLine("New incoming edge failed: {0}", sx);
	      }
            }
            else {
              TcpEdge e = null;
              lock( _sync ) {
                if( s != null && _sock_to_edge.Contains(s) ) {
                  e = (TcpEdge)_sock_to_edge[s];
#if POB_DEBUG
                  Console.Error.WriteLine("DoReceive: {0}", e);
#endif
                }
              }
              //It is really important not to lock across this function call
              if( e != null ) { e.DoReceive(); }
            }
          }
        }//End of readsocks
        if( writesocks != null ) {
          foreach(Socket s in writesocks)
          {
            if( s == _listen_sock ) {

            }
            else {
              TcpEdge e = null;
              lock( _sync ) {
                if( s != null && _sock_to_edge.Contains(s) ) {
                  e = (TcpEdge)_sock_to_edge[s];
#if POB_DEBUG
                  Console.Error.WriteLine("DoSend: {0}", e);
#endif
                }
              }
              //It is really important not to lock across this function call
              if( e != null ) { e.DoSend(); }
            }
          }
        } //End of writesocks check
      }//End of infinite while loop

      //If we are here, we will never hear or send packets again, so, lets close the sockets:
      lock( _sync ) {
        foreach(Socket s in _sock_to_edge.Keys) {
          s.Close();
        }
        //Close the main socket:
        _listen_sock.Close();
      }
    }

    protected void CloseHandler(object edge, EventArgs arg)
    {
      TcpEdge e = (TcpEdge)edge;
      Socket s = e.Socket;
      lock( _sync ) {
        _sock_to_edge.Remove(s);
        _all_sockets.Remove(s);
        _send_sockets.Remove(s);
      }
      s.Close();
    }
    
    /**
     * TcpEdge objects call this method when their
     * send state changes (from true to false or vice-versa).
     */
    public void SendStateChange(TcpEdge e)
    {
      lock( _sync ) {
        if( e.NeedToSend ) {
          _send_sockets.Add(e.Socket);
        }
        else {
          _send_sockets.Remove(e.Socket);
        }
      }
    }

  }

}
