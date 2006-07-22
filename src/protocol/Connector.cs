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

using System;
using System.Collections;

namespace Brunet
{

  /**
   * sends ConnectToMessage objects out onto the network.
   * This sends the request, and then waits for the response.
   * When it gets the reponse, it creates a linker to link the
   * two nodes.  Once it has completed its job, it sends a FinishEvent.
   *
   * This should *ONLY* be used by ConnectionOverlord subclasses.  This
   * is a very low-level class that has to do with bootstrapping and
   * making sure the nodes have the proper neighbors.
   * 
   * @see CtmRequestHandler
   * @see StructuredConnectionOverlord
   * @see UnstructuredConnectionOverlord
   */

  public class Connector : TaskWorker, IAHPacketHandler
  {

    /*private static readonly log4net.ILog _log =
        log4net.LogManager.GetLogger(System.Reflection.MethodBase.
        GetCurrentMethod().DeclaringType);*/
#if PLAB_LOG
    protected BrunetLogger _logger;
    public BrunetLogger Logger{
      get{
        return _logger;
      }
      set
      {
        _logger = value;
      }
    }
#endif


    protected bool _is_finished;
    override public bool IsFinished {
      get {
        lock( _sync ) {
          return _is_finished;
        }
      }
    }

    protected Node _local_node;
    /**
     * The node who is making the Connection request
     */
    public Node Node
    {
      get
      {
        return _local_node;
      }
    }

    protected AHPacket _con_packet;
    /**
     * The packet which is being sent out on the network whose
     * payload contains (a potentially PacketForwarder wrapped)
     * ConnectToMessage
     */
    public AHPacket Packet
    {
      get
      {
        return _con_packet;
      }
    }

    protected ArrayList _got_ctms;
    /**
     * Each received CTM is put on this array.  This is
     * so when the finish event is fired, we can see what
     * the received CTMs were
     */
    public ArrayList ReceivedCTMs
    {
      get { return _got_ctms; }
    }

    protected ConnectToMessage _ctm;
    /** Holds the ConnectToMessage whose response we are looking for */
    public ConnectToMessage Ctm
    {
      get { return _ctm; }
    }

    protected int _ctm_send_timeouts;

    protected ConnectionMessageParser _cmp;
    protected ConnectionOverlord _co;

    /**
     * How many time outs are allowed before assuming failure
     */
    protected readonly int MaxTimeOuts = 3;
    /**
     * The AH Network is slower than IP, give it a little
     * while to get to the end node
     */
    protected static readonly int AHMsTimeOut = 10000;
    protected static readonly TimeSpan _timeout;
    protected DateTime _last_packet_datetime;

    /**
     * Either a Node or an Edge to use to send the
     * ConnectToMessage packet
     */
    protected IPacketSender _sender;
    /**
     * Is false until we get a response
     */
    protected bool _got_ctm;
    /**
     * We lock this when we need thread safety
     */
    protected object _sync;
    public Object SyncRoot {
      get {
	return _sync;
      }
    }
    static Connector() {
      _timeout = new TimeSpan(0, 0, 0, 0, AHMsTimeOut);
    }
    /**
     * Represents the Task this connector works on for the TaskWorker
     */
    protected class ConnectorTask {
      protected IPacketSender _ips;
      protected Address _target;
      public ConnectorTask(IPacketSender ps, Address target) {
        _ips = ps;
        _target = target;
      }

      override public int GetHashCode() {
        return _ips.GetHashCode();
      }
      override public bool Equals(object o) {
        ConnectorTask ct = o as ConnectorTask;
        bool eq = false;
        if( ct != null ) {
          eq = _target.Equals( ct._target );
          if( _target is DirectionalAddress ) {
            /*
             * We are more specific for directional addresses
             * because they are relative to particular nodes
             */
            eq &= ( ct._ips == _ips ) ;
          }
        }
        return eq;
      }
    }
    protected object _task;
    override public object Task { get { return _task; } }

    /**
     * Before a Connector goes to work, it optionally calls
     * this method to see if it is still needed.
     */
    public delegate bool AbortCheck(Connector c);
    protected AbortCheck _abort;
    public AbortCheck AbortIf {
      get {
        return _abort;
      }
      set {
        _abort = value;
      }
    }

    /**
     * @param local the local Node to connect to the remote node
     * @param eh EventHandler to call when we are finished.
     * @param IPacketSender Use this specific edge.  This is used when we want to
     * connecto to a neighbor of a neighbor
     * @param request_packet the packet to send which already contains a CTM
     * @param ctm the ConnectToMessage which is serialized in the packet
     */
    public Connector(Node local, IPacketSender ps, AHPacket request_packet,
                        ConnectToMessage ctm, ConnectionOverlord co)
    {
      _sync = new Object();
      _local_node = local;
      _is_finished = false;

      _got_ctms = new ArrayList();
      _got_ctm = false;
      _cmp = new ConnectionMessageParser();
      _sender = ps;
      _ctm = ctm;
      _co = co;
      _con_packet = request_packet;
      _task = new ConnectorTask(ps, request_packet.Destination);
    }
    /**
     * @param local the local Node to connect to the remote node
     * @param eh EventHandler to call when we are finished.
     * connecto to a neighbor of a neighbor
     * @param request_packet the packet to send which already contains a CTM
     * @param ctm the ConnectToMessage which is serialized in the packet
     */
    public Connector(Node local, AHPacket request_packet,
                        ConnectToMessage ctm, ConnectionOverlord co)
                        : this(local, local, request_packet, ctm, co)
    {

    }

    /**
     * Begin the Connector task of sending ConnectToMessage objects
     * and waiting for the responses
     */
    override public void Start()
    {
      if( _abort != null ) {
        if( _abort(this) ) {
          //We are no longer needed:
          _is_finished = true;
          FireFinished();
          return;
        }
      }
      lock(_sync) {
        
        ///Listen for response to what we send :
        _local_node.Subscribe(AHPacket.Protocol.Connection, this);
        
        _ctm_send_timeouts = 0;
        //_log.Info("Sending CTM Request:");
        //_log.Info("CTM Packet:\n" + request_packet.ToString());
#if PLAB_LOG
        if(request_packet.PayloadType == AHPacket.Protocol.Forwarding){
            AHPacket tmp_pack = PacketForwarder.UnwrapPacket(request_packet);
            BrunetEventDescriptor bed1 = new BrunetEventDescriptor();      
            bed1.RemoteAHAddress = request_packet.Destination.ToBigInteger().ToString();
            bed1.EventDescription = "Connector.Connect.forwarder";
            Logger.LogAttemptEvent( bed1 );
            
            BrunetEventDescriptor bed2 = new BrunetEventDescriptor();      
            bed2.RemoteAHAddress = tmp_pack.Destination.ToBigInteger().ToString();
            bed2.EventDescription = "Connector.Connect.target";
            Logger.LogAttemptEvent( bed2 );
        }                              
#endif

#if ARI_CTM_DEBUG
	Console.WriteLine("Connector - Send CTM request; target: {0}.", 
			  _con_packet.Destination);
#endif
        _sender.Send(_con_packet);
        _last_packet_datetime = DateTime.Now;
        _ctm_send_timeouts = 1;
        _local_node.HeartBeatEvent += new EventHandler(this.ResendCtmHandler);
      }
    }

    /**
     * When we listen for responses to our ConnectToMessages,
     * we must implement this method from IAHPacketHandler
     */
    public void HandleAHPacket(object node, AHPacket p, Edge from)
    {
      lock(_sync) {
        try {
          if (p.PayloadType == AHPacket.Protocol.Connection) {
            /*
             * This is an unfortunate architecture because every
             * active connector calls Parse on every packet.  so
             * if there are 5 active connectors, the packet is parsed
             * 5 times.  This has been mitigated to some degree by
             * the implementation of a cache system inside
             * ConnectionMessageParser.  Ideally, we could check
             * to see if this is the packet we are looking for
             * without decoding XML.  Perhaps using the ReqrepManager
             */
            ConnectionMessage cm = _cmp.Parse(p);
            if ((cm != null) &&
                (cm.Id == _ctm.Id) &&
                (cm.Dir == ConnectionMessage.Direction.Response) &&
                (cm is ConnectToMessage)) {
              /**
              * This is our response.  Now we know who to connect
              * to!
              * @todo see if the type of connection is the same
               */
#if ARI_CTM_DEBUG
	      Console.WriteLine("Got CTM Response: " + cm.ToString());
              Console.WriteLine("Initiating a linking protocol on response... ");
#endif
              ConnectToMessage new_ctm = (ConnectToMessage)cm;
              _got_ctm = true;
              /**
               * It is the responsibilty of the ConnectionOverlord
               * to deal with this ctm
               */
              _got_ctms.Add(new_ctm);
              _co.HandleCtmResponse(this, p, new_ctm);
              //_log.Info("Got CTM Response: " + cm.ToString());
	    }
	  }
	}
        catch(Exception x) {
          //_log.Error(x);
        }
      }
    }

    /**
     * An event handler that gets called periodically by Node.
     */
    protected void ResendCtmHandler(object node, EventArgs arg)
    {
      try {
       lock( _sync ) {
        if( (_abort != null) && _abort(this) ) {
          //It is okay to stop now
          _is_finished = true; 
        }
        else if( DateTime.Now - _last_packet_datetime > _timeout) {
          if( _ctm_send_timeouts >= MaxTimeOuts ) {
            _is_finished = true;
          }
          else if( _got_ctm == false && _ctm_send_timeouts < MaxTimeOuts ) {
            //There has been no response, resend the request
            //Console.WriteLine("Resending:({0})\n{1}", _ctm_send_timeouts,
            //                                          _con_packet);
            _sender.Send( _con_packet );
          }
          _last_packet_datetime = DateTime.Now;
          //We have timed out one more time
          _ctm_send_timeouts++;
        }
       }
      }
      catch(Exception x) {
        _is_finished = true;
      }
      finally {
        if( _is_finished ) {
          //We are done now:
          _local_node.HeartBeatEvent -= new EventHandler(this.ResendCtmHandler);
          //Now we have the response :  stop listening
          _local_node.Unsubscribe(AHPacket.Protocol.Connection, this);
          if( _is_finished ) {
            FireFinished();
          }
        }
      }
    }

  }

}



