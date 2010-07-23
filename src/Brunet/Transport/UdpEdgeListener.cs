/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2005,2006  P. Oscar Boykin <boykin@pobox.com>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using Brunet;
using Brunet.Concurrent;
using Brunet.Collections;
using Brunet.Util;
using System;
using System.IO;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Collections;
using System.Collections.Generic;

namespace Brunet.Transport
{
  /**
   * A EdgeListener that uses UDP for the underlying
   * protocol.  This listener creates UDP edges.
   * 
   * The UdpEdgeListener creates two threads, one for reading from the socket
   * and the other writing to the socket.  Tests suggest that having a single
   * thread for writing improves bandwidth and latency performance over using 
   * asynchronous sockets or calling a send over a threadpool.
   */
  public class UdpEdgeListener : EdgeListener, IEdgeSendHandler
  {
    /////////
    // Inner classes
    /////////
    protected enum ControlCode : int {
      EdgeClosed = 1,
      EdgeDataAnnounce = 2, ///Send a dictionary of various data about the edge
      Null = 3 ///This is a null message, it means just ignore the packet
    }

    /*
     * Holds the information needed to send a packet
     * to make sure only one Send happens at a time
     * READ THIS: Linux sometimes blocks for seconds on UDP send
     * if the sends are not in their own thread, that blocks the whole
     * system.
     */
    protected sealed class SendState {
      public readonly int LocalID;
      public readonly int RemoteID;
      public readonly ICopyable Data;
      public readonly EndPoint End;
      public SendState(int l, int r, ICopyable d, EndPoint e) {
        LocalID = l;
        RemoteID = r;
        Data = d;
        End = e;
      }
    }
  
    /**
     * Ensures that only one write is happening at a time without locks.
     * READ THIS: Linux sometimes blocks for seconds on UDP send
     * if the sends are not in their own thread, that blocks the whole
     * system.  IF you decide to change this, REMEMBER!
     */
    protected sealed class SendServer {
      private readonly Socket _socket;
      private readonly byte[] _buffer;
      private readonly LFBlockingQueue<SendState> _queue;
      private readonly int MAX = 512;
      public SendServer(Socket s, byte[] buffer) {
        _socket = s;
        _buffer = buffer;
        _queue = new LFBlockingQueue<SendState>();
      }
      public bool Add(SendState ss) {
        if( _queue.Count < MAX ) {
          _queue.Enqueue(ss);
          return true;
        }
        else {
          return false;
        }
      }
      public void Run() {
        bool got;
        SendState ss = _queue.Dequeue(-1, out got);
        while( null != ss ) {
          Serve(ss);
          ss = _queue.Dequeue(-1, out got);
        }
      }
      public void Stop() {
        _queue.Enqueue(null);
      }
      //This method should never throw an exception
      private void Serve(SendState state) {
        //Write the IDs of the edge:
        //[local id 4 bytes][remote id 4 bytes][packet]
        try {
          NumberSerializer.WriteInt(state.LocalID, _buffer, 0);
          NumberSerializer.WriteInt(state.RemoteID, _buffer, 4);
          int plength = state.Data.CopyTo(_buffer, 8);
          _socket.SendTo(_buffer, 8 + plength, SocketFlags.None, state.End);
        }
        catch(Exception x) {
          if(ProtocolLog.Exceptions.Enabled) {
            ProtocolLog.Write(ProtocolLog.Exceptions, x.ToString());
          }
        }
      }
    }
    /** Holds any extra information we need to keep for each Edge
     * in the future, this may hold information needed to manage
     * timing the edge out, etc...
     */
    protected sealed class EdgeState {
      public readonly UdpEdge Edge;
      public EdgeState(UdpEdge e) {
        if( e == null ) {
          throw new ArgumentNullException("Edge cannot be null in EdgeState");
        }
        Edge = e;
      }
    }

    /** Here is all the mutable state information for the EdgeListener
     * All instances of this will be kept in the ListenThread so
     * there is no need for thread-safety here.
     */
    protected sealed class ListenerState {
      public readonly UidGenerator<EdgeState> LocalIdTab;
      public readonly Dictionary<int, List<EdgeState> > RemoteIdTab;
      public readonly UdpEdgeListener EL;
      public TAAuthorizer TAAuth;

      //Private data:
      private EndPoint End;
      private readonly Socket Sock;
      private readonly BufferAllocator BA;

      public ListenerState(UdpEdgeListener el, Socket s, TAAuthorizer taa) {
        EL = el;
        Sock = s;
        TAAuth = taa;
        End = new IPEndPoint(IPAddress.Any, 0);
        BA = new BufferAllocator(8 + Int16.MaxValue);
        //Don't allocate negative local IDs:
        var rand = new SecureRandom();
        LocalIdTab = new UidGenerator<EdgeState>(rand, true);
        RemoteIdTab = new Dictionary<int, List<EdgeState> >();
      }
      private void AddRemoteTab(EdgeState es) {
        int remoteid = es.Edge.RemoteID;
        List<EdgeState> remotes;
        if( false == RemoteIdTab.TryGetValue(remoteid, out remotes) ) {
          //First one of this id:
          remotes = new List<EdgeState>();
          RemoteIdTab.Add(remoteid, remotes);
        }
        remotes.Add(es);
      }
      //TODO we need to think about security more here: 
      private bool CheckEndValidity(UdpEdge edge) {
        if( edge.End.Equals(End) ) {
          //Usual case:
          return true;
        }
        else {
          /*
           * This is either legitimate: due to mobility/NAT change
           * or an attack and it is session hijacking.  Now, we
           * assume all is good.  It would probably be better not
           * to be so trusting.
           * TODO one idea: ping the old edge.End and see if someone responds quickly
           * if so, assume this is an attack ignore the mapping change, if not, assume
           * all is good, and accept the mapping change
           */
          if(ProtocolLog.UdpEdge.Enabled) {
            ProtocolLog.Write(ProtocolLog.UdpEdge, String.Format(
              "Remote NAT Mapping changed on Edge: {0}\n{1} -> {2}",
              edge, edge.End, End)); 
          }
          //Actually update:
          var rta = TransportAddressFactory.CreateInstance(TransportAddress.TAType.Udp,(IPEndPoint)End);
          if( TAAuth.Authorize(rta) != TAAuthorizer.Decision.Deny ) {
            IPEndPoint this_end = (IPEndPoint)End;
            /*
             * .Net overwrites the End variable (passed by ref) into the Socket
             * we explicitly copy here to make it clear that the End can't change
             * by being passed to the socket
             */
            edge.End = new IPEndPoint(this_end.Address, this_end.Port);
            var dp = new RemoteMappingChangePoint(DateTime.UtcNow, edge);
            EL._pub_state.Update(new AddNatData(dp));
            //Tell the other guy:
            EL.SendControlPacket(edge, End, edge.RemoteID, edge.ID, ControlCode.EdgeDataAnnounce);
            return true;
          }
          else {
            /*
             * Looks like the new TA is no longer authorized.
             * //TODO SECURITY:
             * If someone sends a packet from a unauthorized TA with the matching local/remoteid
             * they can close the edge.
             */
            EL.SendControlPacket(edge, End, edge.RemoteID, edge.ID, ControlCode.EdgeClosed);
            EL.RequestClose(edge);
            RemoveEdge(edge);
            return false;
          }
        }
      }
      public void CloseAllEdges() {
        foreach(var edgestate in LocalIdTab) {
          EL.RequestClose(edgestate.Edge); 
        }
      }
      public UdpEdge CreateEdge(int remoteid, IPEndPoint end) {
        bool is_incoming = (remoteid != 0);
        var id_edge = LocalIdTab.GenerateID(delegate(int id) {
          UdpEdge ue = new UdpEdge(EL, is_incoming, end, EL.LocalEndPoint, id, remoteid);
          /* Tell me when you close so I can clean up the table */
          ue.CloseEvent += EL.CloseHandler;
          return new EdgeState(ue);
        });
        UdpEdge new_e = id_edge.Second.Edge;
        if( is_incoming ) {
          AddRemoteTab(id_edge.Second);
        } 
        EL._pub_state.UpdateSeq(
          new IncEdgeCount(), 
          new AddNatData(new NewEdgePoint(DateTime.UtcNow, new_e))
        );
        return new_e;
      }

      private void HandleControlPacket(EdgeState es, int rec_bytes)
      {
        UdpEdge e = es.Edge;
        try {
          ControlCode code = (ControlCode)NumberSerializer.ReadInt(BA.Buffer, BA.Offset + 8);
          //4+4 (remote + local id) + 4 (control code) = 12 bytes to skip
          var control_payload = MemBlock.Reference(BA.Buffer, BA.Offset + 12, rec_bytes - 12);
          if(ProtocolLog.UdpEdge.Enabled)
            ProtocolLog.Write(ProtocolLog.UdpEdge, String.Format(
              "Got control {1} from: {0}", e, code));
          if( code == ControlCode.EdgeClosed ) {
            //The edge has been closed on the other side
            EL.RequestClose(e);
            RemoveEdge(e);
          }
          else if( code == ControlCode.EdgeDataAnnounce ) {
            //our NAT mapping may have changed:
            var info = (IDictionary)AdrConverter.Deserialize( control_payload );
            var our_local_ta = (string)info["RemoteTA"]; //his remote is our local
            if( our_local_ta != null ) {
              //Update our list:
              var new_ta = TransportAddressFactory.CreateInstance(our_local_ta);
              var old_ta = e.PeerViewOfLocalTA;
              if( ! new_ta.Equals( old_ta ) ) {
                if(ProtocolLog.UdpEdge.Enabled)
                  ProtocolLog.Write(ProtocolLog.UdpEdge, String.Format(
                    "Local NAT Mapping changed on Edge: {0}\n{1} => {2}",
                 e, old_ta, new_ta));
                //Looks like matters have changed:
                EL.UpdateLocalTAs(e, new_ta);
                /**
                 * @todo, maybe we should ping the other edges sharing this
                 * EndPoint, but we need to be careful not to do some O(E^2)
                 * operation, which could easily happen if each EdgeDataAnnounce
                 * triggered E packets to be sent
                 */
              }
            }
          }
          else if( code == ControlCode.Null ) {
            //Do nothing in this case
          }
        }
        catch(Exception x) {
        //This could happen if this is some control message we don't understand
          if(ProtocolLog.Exceptions.Enabled)
            ProtocolLog.Write(ProtocolLog.Exceptions, x.ToString());
        }
      }
      private void HandleDataPacket(EdgeState es, int rec_bytes) {
        UdpEdge e = es.Edge;
        if( CheckEndValidity(e) ) {
          //This is the normal case, a packet for us
          try {
            e.ReceivedPacketEvent(TakePacket(rec_bytes));
          }
          catch(EdgeClosedException) {
            RemoveEdge(e);
            EL.SendControlPacket(e, End, e.RemoteID, e.ID, ControlCode.EdgeClosed);
          }
        }
        //else: We just ignore this...
      }
      private void HandleMismatch(int local, int remote, int rec_bytes) {
        //TODO this could be a security issue
        //better to keep a cache of recently used localid to see if this is
        //legit, and ignore otherwise
        EL.SendControlPacket(null, End, remote, local, ControlCode.EdgeClosed);
      }
      private void HandleNewEdgeReq(int remoteid, int rec_bytes) {
        /*
         * We copy the endpoint because (I think) .Net
         * overwrites it each time.  Since making new
         * edges is rare, this is better than allocating
         * a new endpoint each time
         */
        IPEndPoint this_end = (IPEndPoint)End;
        IPEndPoint my_end = new IPEndPoint(this_end.Address,
                                           this_end.Port);
        UdpEdge e = CreateEdge(remoteid, my_end);
        try {
          EL.SendEdgeEvent(e);
          e.ReceivedPacketEvent(TakePacket(rec_bytes));
        }
        catch {
          RemoveEdge(e);
          EL.SendControlPacket(e, End, remoteid, 0, ControlCode.EdgeClosed);
        }
      }
      /**
       * This is a System.Threading.ThreadStart delegate
       * We loop waiting for edges that need to send,
       * or data on the socket.
       *
       * This is the only thread that can touch the socket,
       * therefore, we do not need to lock the socket.
       */
      public void ListenThread()
      {
        Thread.CurrentThread.Name = "udp_listen_thread";
        var ps = EL._pub_state; 
        
        //Variables to track logging:
        DateTime last_debug = DateTime.UtcNow;
        DateTime now;
        int debug_period = 5000;
        bool logging = ProtocolLog.Monitor.Enabled;
  
        //Here is the local-state-only model for threading:
        ImmutableList<IListenerAction> act_stack;
        //Here is the action loop:
        while(ps.State.RunState == 1) {
          if(logging) {
            now = DateTime.UtcNow;
            if(last_debug.AddMilliseconds(debug_period) < now) {
              last_debug = now;
              ProtocolLog.Write(ProtocolLog.Monitor, String.Format("I am alive: {0}", now));
            }
          }
          try {
            ProcessNextPacket();
            //See if there are any pending actions to take:
            while( EL._actions.TryPop(out act_stack) ) {
              //Process the head:
              act_stack.Head.Start(this);
            }
            //The action stack is empty, let's wait for the next packet or action
          }
          catch(SocketException sx) {
            /*
             * Socket exceptions sometimes happen on nodes with poorly configured IP
             * or other kernel problems.  We probably don't want to stop running on
             * that case, so we just print the exception and continue
             */
            if((ps.State.RunState == 1) && ProtocolLog.Exceptions.Enabled) {
              ProtocolLog.Write(ProtocolLog.Exceptions, sx.ToString());
            }
          }
          catch(Exception x) {
            /*
             * This is never expected.  Let's print the exception and quit
             */
            Console.Error.WriteLine(
              "Exception in UdpEdgeListener(port={0}).ListenThread: {1}", EL._port, x
            );
            ps.Update(new StopUpdater());
          }
        }
        CloseAllEdges();
        Sock.Close();
        ps.Update(new Finish());
      }
      public void ProcessNextPacket() {
        int rec_bytes = Sock.ReceiveFrom(BA.Buffer, BA.Offset, BA.Capacity,
                                         SocketFlags.None, ref End);
        if( rec_bytes > 8 ) {
          int remoteid = NumberSerializer.ReadInt(BA.Buffer, BA.Offset);
          int localid = NumberSerializer.ReadInt(BA.Buffer, BA.Offset + 4);

          EdgeState es;
          if( LocalIdTab.TryGet(localid, out es) ) {
            //This is the most common case, so try it first
            UdpEdge e = es.Edge;
            int old_rem = e.TrySetRemoteID(remoteid);
            if( old_rem == remoteid ) {
              HandleDataPacket(es, rec_bytes);
            }
            else if( old_rem == 0 ) {
              //This is the first packet we've heard from our edge creation!
              AddRemoteTab(es);
              HandleDataPacket(es, rec_bytes);
            }
            else {
              //remoteid does not match:
              HandleMismatch(localid, remoteid, rec_bytes);
            }
          }
          else if( localid == 0 ) {
            //This is a special id to request a new edge
            HandleNewEdgeReq(remoteid, rec_bytes);
          }
          else if( localid < 0 ) {
            //This is a control message
            int plocalid = ~localid;
            bool have_edge = LocalIdTab.TryGet(plocalid, out es);
            if( have_edge ) {
              int old_rem = es.Edge.TrySetRemoteID(remoteid);
              if( old_rem == remoteid ) { 
                HandleControlPacket(es, rec_bytes);
              }
              else if( old_rem == 0 ) {
                AddRemoteTab(es);
                HandleControlPacket(es, rec_bytes);
              }
              else {
                //remoteid doesn't match
                HandleMismatch(localid, remoteid, rec_bytes);
              } 
            }
            else {
              //No such local id:
              HandleMismatch(localid, remoteid, rec_bytes);
            }
          }
          else {
            //localid > 0, but we don't know about it:
            HandleMismatch(localid, remoteid, rec_bytes);
          }
        }
        //else we didn't receive enough to be meaningful
      }
      public void RemoveEdge(UdpEdge e) {
          EdgeState es;
          if( LocalIdTab.TryTake( e.ID, out es ) ) {
            List<EdgeState> remotes;
            int rem = e.RemoteID;
            if( RemoteIdTab.TryGetValue(rem, out remotes) ) {
              remotes.Remove(es);
              if( remotes.Count == 0 ) {
                //Clean up:
                RemoteIdTab.Remove(rem);
              }
            }
            EL._pub_state.UpdateSeq(
              new DecEdgeCount(), 
              new AddNatData( new EdgeClosePoint(DateTime.UtcNow, e))
            );
          }
          //else: This edge has already been closed
      }
      /** Advance the buffer and return the packet
       * @param rec_bytes the total number of bytes received (including id bytes)
       */
      private MemBlock TakePacket(int rec_bytes) {
        var packet_buffer = MemBlock.Reference(BA.Buffer, BA.Offset + 8, rec_bytes - 8);
        BA.AdvanceBuffer(rec_bytes);
        return packet_buffer;
      }
    }
    
    /**
     * Here are all the ways we can modify the ListenerState
     * These are actions initiated OUTSIDE the ListenThread.
     * The listen thread can just call methods on ListenerState
     * directly.
     */
    protected interface IListenerAction {
      void Start(ListenerState la);
    }

    protected class SetAuthAction : IListenerAction {
      readonly TAAuthorizer TAA;
      public SetAuthAction(TAAuthorizer taa) {
        TAA = taa;
      }
      public void Start(ListenerState ls) {
        ls.TAAuth = TAA;
        var bad_edges = new List<UdpEdge>();
        foreach(EdgeState es in ls.LocalIdTab) {
          if( TAA.Authorize( es.Edge.RemoteTA ) == TAAuthorizer.Decision.Deny ) {
            bad_edges.Add(es.Edge);
          }
        }
        //Close the newly bad Edges.
        foreach(UdpEdge e in bad_edges) {
          ls.EL.RequestClose(e);
          ls.RemoveEdge(e);
        }
      }
    }
    protected class CloseAction : IListenerAction {
      public readonly UdpEdge Edge;
      public CloseAction(UdpEdge e) {
        Edge = e;
      }
      public void Start(ListenerState ls) {
        ls.RemoveEdge(Edge);
      }
    }
    protected class CreateAction : IListenerAction {
      public readonly EdgeCreationCallback ECB;
      public readonly TransportAddress TA;
      public CreateAction(TransportAddress ta, EdgeCreationCallback ecb) {
        TA = ta;
        ECB = ecb;
      }
      public void Start(ListenerState ls) {
        UdpEdge new_e = null;
        Exception ex = null;
        bool success;
        try {
          if( ls.TAAuth.Authorize(TA) == TAAuthorizer.Decision.Deny ) {
            //Too bad.  Can't make this edge:
	    throw new EdgeException( TA.ToString() + " is not authorized");
          }
          IPAddress first_ip = ((IPTransportAddress) TA).GetIPAddress();
          var end = new IPEndPoint(first_ip, ((IPTransportAddress) TA).Port);
          //remote id is zero on a newly created edge
          new_e = ls.CreateEdge(0, end);
          success = true;
        } 
        catch(Exception x) {
          ex = x;
          success = false;
        }
        ECB(success, new_e, ex);
      }
    }
    /*
     * This does nothing an is only used to wake up the 
     * the listen thread when we stop
     */
    protected sealed class NullAction : IListenerAction {
      public void Start(ListenerState ls) { }
    }

    /** This is the immutable state that can be publicly read.
     */
    protected sealed class PublicState {
      public readonly NatHistory NatHist;
      public readonly IEnumerable NatTAs;
      public readonly IEnumerable TAs;
      public readonly int EdgeCount;
      /* 0 -> not started
       * 1 -> started and running
       * 2 -> started then stopped
       * 3 -> the listen thread has finished
       */
      public readonly int RunState; 
      public PublicState(NatHistory nh, IEnumerable nt, IEnumerable tas, int edgecnt, int runs) {
        NatHist = nh;
        NatTAs = nt;
        TAs = tas;
        EdgeCount = edgecnt;
        RunState = runs;
      }
    }
    // Here all all the ways we can modify the PublicState
    protected sealed class IncEdgeCount : Mutable<PublicState>.Updater {
      public PublicState ComputeNewState(PublicState ps) {
        return new PublicState(ps.NatHist, ps.NatTAs, ps.TAs,
                               ps.EdgeCount + 1, ps.RunState);
      }
    }
    protected sealed class DecEdgeCount : Mutable<PublicState>.Updater {
      public PublicState ComputeNewState(PublicState ps) {
        return new PublicState(ps.NatHist, ps.NatTAs, ps.TAs,
                               ps.EdgeCount - 1, ps.RunState);
      }
    }
    protected sealed class Finish : Mutable<PublicState>.Updater {
      public PublicState ComputeNewState(PublicState ps) {
        if( ps.RunState >= 1 ) {
          return new PublicState(ps.NatHist, ps.NatTAs, ps.TAs,
                                 ps.EdgeCount, 3);
        }
        else {
          throw new Exception("Can't finish before we start");
        }
      }
    }
    protected sealed class StartUpdater : Mutable<PublicState>.Updater {
      public PublicState ComputeNewState(PublicState ps) {
        if( ps.RunState == 0 ) {
          return new PublicState(ps.NatHist, ps.NatTAs, ps.TAs,
                                 ps.EdgeCount, 1);
        }
        else {
          throw new Exception("UdpEdgeListener Restart not allowed");
        }
      }
    }
    protected sealed class StopUpdater : Mutable<PublicState>.Updater {
      public PublicState ComputeNewState(PublicState ps) {
        if( ps.RunState == 1 ) {
          return new PublicState(ps.NatHist, ps.NatTAs, ps.TAs,
                                 ps.EdgeCount, 2);
        }
        else if( ps.RunState == 2) { 
          //We are calling stop a second time, idempotent:
          return ps;
        }
        else {
          throw new Exception("UdpEdgeListener not yet started");
        }
      }
    }
    protected sealed class AddNatData : Mutable<PublicState>.Updater {
      public readonly NatDataPoint NDP;
      public AddNatData(NatDataPoint ndp) {
        NDP = ndp;
      }
      public PublicState ComputeNewState(PublicState ps) {
        NatHistory new_nh = ps.NatHist + NDP;
        NatTAs new_nta = new NatTAs(ps.TAs, new_nh);
        return new PublicState(new_nh, new_nta, ps.TAs,
                               ps.EdgeCount, ps.RunState);
      }
    }

    /////////
    // Member Variables 
    /////////

    /*
     * NOTE: all of these are readonly, we never change any
     * of these references.  The socket is not thread-safe, 
     * but we use the SendServer and the ListenerState to modify
     * it.
     */
    private readonly SendServer _send_server;
    private readonly Mutable<PublicState> _pub_state;
    private readonly LockFreeStack<IListenerAction> _actions;
    protected readonly Thread _listen_thread;
    protected readonly int _port;
    protected readonly IPEndPoint LocalEP; 
    
    /////////
    // Properties
    /////////

    public override IEnumerable LocalTAs {
      get {
        return _pub_state.State.NatTAs;
      }
    }

    public override TransportAddress.TAType TAType {
      get {
        return TransportAddress.TAType.Udp;
      }
    }

    public override int Count { get { return _pub_state.State.EdgeCount; } }

    public override bool IsStarted {
      get { return _pub_state.State.RunState >= 1; }
    }

    //This is our best guess of the local endpoint
    public IPEndPoint LocalEndPoint { get { return GuessLocalEndPoint(_port, _pub_state.State.TAs); } }


    override public TAAuthorizer TAAuth {
      /**
       * When we add a new TAAuthorizer, we have to check to see
       * if any of the old addresses are no good, in which case, we
       * close them
       */
      set {
        //Next time we receive a packet we'll update:
        _actions.Push(new SetAuthAction(value));
      }
    }
    
    /////////
    // Constructors
    /////////

    public UdpEdgeListener() : this(0, null, null)
    {
    }

    public UdpEdgeListener(int port)
    : this(port, null, null)
    {
    }
    public UdpEdgeListener(int port, IEnumerable ips)
       : this(port, ips, null)  { }
    /**
     * @param port the local port to bind to
     * @param local_tas an IEnumerable object which gives the list of local
     * IPs.  This is consulted every time LocalTAs is accessed, so it can
     * change as new interfaces are added
     * @param ta_auth the TAAuthorizer for packets incoming
     */
    public UdpEdgeListener(int port, IEnumerable local_config_ips, TAAuthorizer ta_auth)
    {
      //Create the socket we will use:
      var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
      var ipep = new IPEndPoint(IPAddress.Any, port);
      s.Bind(ipep);
      
      //This manages sending on the socket:
      _send_server = new SendServer(s, new byte[8 + Int16.MaxValue]);
     
      //Manage the listening on the socket: 
      if( ta_auth == null ) {
        //Always authorize in this case:
        ta_auth = new ConstantAuthorizer(TAAuthorizer.Decision.Allow);
      }
      //Don't keep a reference to this, we want to let it live in the other thread:
      var ls = new ListenerState(this, s, ta_auth);
      _listen_thread = new Thread( ls.ListenThread );
      _actions = new LockFreeStack<IListenerAction>(); 
      
      //Set up the public state:
      /**
       * We get all the IPAddresses for this computer
       */
      _port = ((IPEndPoint) (s.LocalEndPoint)).Port;
      LocalEP = new IPEndPoint(IPAddress.Loopback, _port);
      IEnumerable tas;
      if( local_config_ips == null ) {
        tas = TransportAddressFactory.CreateForLocalHost(TransportAddress.TAType.Udp, _port);
      }
      else {
        tas = TransportAddressFactory.Create(TransportAddress.TAType.Udp, _port, local_config_ips);
      }
      //Set up the public state:
      NatHistory nh = null;
      int edgecount = 0; //no edges yet
      int runstate = 0; //not yet started
      var ps = new PublicState(nh, new NatTAs(tas, nh), tas, edgecount, runstate);
      _pub_state = new Mutable<PublicState>(ps);
    }

    /////////
    // Methods
    /////////
    
    /**
     * When a UdpEdge closes we need to remove it from
     * our table, so we will know it is new if it comes
     * back.
     */
    public void CloseHandler(object edge, EventArgs args)
    {
      //Eventually, this packet will make it into the listen thread:
      _actions.Push(new CloseAction((UdpEdge)edge));
    }
    /**
     * Implements the EdgeListener function to 
     * create edges of this type.
     */
    public override void CreateEdgeTo(TransportAddress ta, EdgeCreationCallback ecb)
    {
      try {
        if( !IsStarted ) {
	  throw new EdgeException("UdpEdgeListener is not started");
        }
        if( ta.TransportAddressType != this.TAType ) {
	  throw new EdgeException(ta.TransportAddressType.ToString()
				+ " is not my type: " + this.TAType.ToString() );
        }
        _actions.Push(new CreateAction(ta, ecb));
        WakeListen();
      }
      catch(Exception x) {
        ecb(false, null, x);
      }
    }

    protected static IPEndPoint GuessLocalEndPoint(int defport, IEnumerable tas) {
      try {
        foreach(IPTransportAddress ta in tas) {
          var a = ta.GetIPAddress();
          if((false == IPAddress.IsLoopback(a)) &&
             (false == IPAddress.Any.Equals(a))) {
            //Here is a good IP address:
            return new IPEndPoint(a, ta.Port);
          }
        }
      }
      catch (Exception x) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, x.ToString());
      }
      return new IPEndPoint(IPAddress.Loopback, defport);
    }

    /**
     * When UdpEdge objects call Send, it calls this packet
     * callback:
     */
    public void HandleEdgeSend(Edge from, ICopyable p) {
      UdpEdge sender = (UdpEdge) from;
      var ss = new SendState(sender.ID, sender.RemoteID, p, sender.End);
      if( false == _send_server.Add(ss) ) {
        //This is a transient error, the queue is full:
        throw new Brunet.Messaging.SendException(true, "UDP Queue full, can't send"); 
      }
    }

    /**
     * When a new Connection is added, we may need to update the list
     * of TAs to make sure it is not too long, and that the it is sorted
     * from most likely to least likely to be successful
     * @param e the new Edge
     * @param ta the TransportAddress our TA according to our peer
     */
    public override void UpdateLocalTAs(Edge e, TransportAddress ta) {
      UdpEdge ue = e as UdpEdge;
      if( null != ue ) {
        ue.PeerViewOfLocalTA = ta;
        _pub_state.Update(
          new AddNatData(new LocalMappingChangePoint(DateTime.UtcNow, e, ta))
        );
      }
    }

    /*
     * send an action into the the listen thread if we are not there already
     */
    private void WakeListen() {
      if( Thread.CurrentThread != _listen_thread ) {
        //Only wake up the ListenThread if we are not
        //already in the listen thread:
        //TODO this assumes a local packet is never lost.
        //if a local packet is lost but no other packets are received,
        //the other threads might not wake up 
        var ss = new SendState(-1, -1, MemBlock.Null, LocalEP);
        _send_server.Add(ss);
        //Send twice, just in case, don't do this a lot
        _send_server.Add(ss);
      }
    }

    protected void SendControlPacket(UdpEdge e, EndPoint end, int remoteid, int localid, ControlCode c) 
    {
      var code = new byte[4];
      NumberSerializer.WriteInt((int)c, code, 0);
      ICopyable data = MemBlock.Reference(code);
      if( c == ControlCode.EdgeDataAnnounce ) {
        if( (e != null) && (e.RemoteID == remoteid) ) {
          Hashtable t = new Hashtable();
          t["RemoteTA"] = e.RemoteTA.ToString();
          t["LocalTA"] = e.LocalTA.ToString();
          data = new CopyList(data, new AdrCopyable(t));
        }
        else {
          if(ProtocolLog.UdpEdge.Enabled)
            ProtocolLog.Write(ProtocolLog.UdpEdge, String.Format(
              "Problem sending EdgeData: EndPoint: {0}, remoteid: {1}, " +
              "localid: {2}, Edge: {3}", end, remoteid, localid, e));
        }
      }
      //Bit flip remote to indicate this is a control packet
      var ss = new SendState(localid, ~remoteid, data, end);
      _send_server.Add(ss);

      if(ProtocolLog.UdpEdge.Enabled) {
        ProtocolLog.Write(ProtocolLog.UdpEdge, String.Format(
          "Sending control {1} to: {0}", end, c));
      }
    }
    /**
     * This method may be called once to start listening.
     * @throw Exception if start is called more than once (including
     * after a Stop
     */
    public override void Start()
    {
      _pub_state.Update(new StartUpdater());
      _listen_thread.Start();
      Thread t = new Thread(_send_server.Run);
      t.Start();
    }

    /**
     * To stop listening, this method is called
     */
    public override void Stop()
    {
      _pub_state.Update(new StopUpdater());
      if( Thread.CurrentThread != _listen_thread ) {
        //Now run has been set to stop, just wake up:
        WakeListen(); 
        _listen_thread.Join();
      }
      _send_server.Stop();
    }
  }
}
