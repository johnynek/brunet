/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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

/*
 * Dependencies:
 * Brunet.Edge;
 * Brunet.EdgeException
 * Brunet.EdgeListener;
 * Brunet.TransportAddress;
 * Brunet.TcpEdge;
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

  public class TcpEdgeListener:EdgeListener
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
    protected ArrayList _tas;

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

    public override ArrayList LocalTAs
    {
      get { return ArrayList.ReadOnly( _tas ); }
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

    public TcpEdgeListener(int port)
    {
      _is_started = false;

      /**
       * We get all the IPAddresses for this computer
       */
      String StrLocalHost =  (Dns.GetHostName());
      IPHostEntry IPEntry = Dns.GetHostByName (StrLocalHost);
      IPAddress [] addr = IPEntry.AddressList;
      _tas = new ArrayList();
      foreach(IPAddress a in IPEntry.AddressList) {
        /**
         * We add Loopback addresses to the back, all others to the front
         * This makes sure non-loopback addresses are listed first.
         */
        if( IPAddress.IsLoopback(a) ) {
          //Put it at the back
          _tas.Add( new TransportAddress(TransportAddress.TAType.Tcp,
                                         new IPEndPoint(a, port) ) );
        }
        else {
          //Put it at the front
          _tas.Insert(0, new TransportAddress(TransportAddress.TAType.Tcp,
                                              new IPEndPoint(a, port) ) );
        }
      }
      _local_endpoint = new IPEndPoint(IPAddress.Any, port);
      _listen_sock = new Socket(AddressFamily.InterNetwork,
                                SocketType.Stream, ProtocolType.Tcp);

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
      CreationState cs = new CreationState(ecb,
                                           new Queue( ta.GetIPAddresses() ),
                                           ta.Port);
      TryNextIP( cs );
    }

    public override void Start()
    {
      lock( _sync ) {
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
      try {
        Socket s = (Socket)ar.AsyncState;
        lock( _sync ) {
          cs = (CreationState)_sock_to_edge[s];
          _sock_to_edge.Remove(s);
        }

        s.EndConnect(ar);
        if (s.Connected) {
          //This new edge is NOT an incoming edge, thus the "false"
          TcpEdge e = new TcpEdge(s, false);
          lock( _sync ) {
            _all_sockets.Add(s);
            _sock_to_edge[s] = e;
          }
          e.CloseEvent += new EventHandler(this.CloseHandler);
          e.SendStateChange += new EventHandler(this.SendStateHandler);
          //Start listening for incoming packets:
          e.Start();
          //We have success:
          cs.ECB(true, e, null);
        }
      }
      catch(Exception x) {
        TryNextIP( cs );
      }
    }

    protected void TryNextIP(CreationState cs)
    {
      if( cs.IPAddressQueue.Count <= 0 ) {
        cs.ECB(false, null, new EdgeException("No more IP Addresses") );
      }
      else {
        EdgeCreationCallback ecb = cs.ECB;
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
                            true);
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
          catch(SocketException x) {
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
                if( _sock_to_edge.Contains(s) ) {
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
                TcpEdge e = new TcpEdge(new_s, true);
                lock( _sync ) {
                  _all_sockets.Add(new_s);
                  _sock_to_edge[new_s] = e;
                }
                e.CloseEvent += new EventHandler(this.CloseHandler);
                e.SendStateChange += new EventHandler(this.SendStateHandler);
                SendEdgeEvent(e);
                e.Start();
	      }
	      catch(SocketException sx) {
                //Looks like this Accept has failed.  Do nothing

	      }
            }
            else {
              TcpEdge e = null;
              lock( _sync ) {
                if( _sock_to_edge.Contains(s) ) {
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
                if( _sock_to_edge.Contains(s) ) {
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
    }
    protected void SendStateHandler(object edge, EventArgs arg)
    {
      lock( _sync ) {
        TcpEdge e = (TcpEdge)edge;
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
