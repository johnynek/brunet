/**
 * Dependencies : 
 * Brunet.Address
 * Brunet.AHPacket
 * Brunet.ConnectionOverlord
 * Brunet.ConnectionTable
 * Brunet.ConnectionType
 * Brunet.Edge
 * Brunet.Linker
 * Brunet.Node
 * Brunet.RwtaAddress
 * Brunet.ConnectToMessage
 * Brunet.ConnectionMessage
 * Brunet.Connector
 * Brunet.TransportAddress
 * Brunet.PacketForwarder
 * Brunet.ConnectionEventArgs
 */

#define KML_DEBUG

using System;
using System.Collections;
//using log4net;
namespace Brunet
{
 /**
  * Makes sure we have at least two unstructured connections at all times.
  * Also, when an unstructured connection is lost, it will be compensated.
  * In addition, a node accepts all requests to make unstructured connections.
  * Therefore, the number of a node's unstructured connection is non-decreasing.
  */
  public class UnstructuredConnectionOverlord:ConnectionOverlord
  {
  /*private static readonly log4net.ILog log =
      log4net.LogManager.GetLogger(System.Reflection.MethodBase.
      GetCurrentMethod().DeclaringType);*/
    
    protected Node _local;
    protected ConnectionTable _connection_table;
    protected ArrayList _remote_ta_list;

    /**
     * These are all the connectors that are currently working.
     * When they are done, their FinishEvent is fired, and we
     * will remove them from this list.  This makes sure they
     * are not garbage collected.
     */
    protected ArrayList _connectors;

    /**
     * An object we lock to get thread synchronization
     */
    protected object _sync;

    // bootstrap the first uc_bootstrap_threshold unstructured connections
    // through a random leaf connection
    protected short uc_bootstrap_threshold = 2; 

    //the total number of desired connections
    //the minimum number is two and this should be a non-decreasing number
    protected short total_desired = 2; 

    //this variable is for keeping track of the 
    //total number of unstructured connections
    protected short total_curr = 0; 

    protected Random _rand;

    protected const short unstructured_connect_ttl = 10;

    public UnstructuredConnectionOverlord(Node local)
    {
      _compensate = false;
      _local = local;
      _connection_table = _local.ConnectionTable;
      _rand = new Random(DateTime.Now.Millisecond);
      _connectors = new ArrayList();
      _sync = new Object();      
   
      lock( _sync ) {
        _local.ConnectionTable.DisconnectionEvent +=
              new EventHandler(this.CheckAndDisconnectHandler);
        _local.ConnectionTable.ConnectionEvent +=
              new EventHandler(this.CheckAndConnectHandler);
      }
    }

    protected bool _compensate;
  /**
   * If we start compensating, we check to see if we need to
   * make a connection : 
   */
    override public bool IsActive
    {
      get
      {
        return _compensate;
      }
      set
      {
        _compensate = value;
      }
    }

  /**
   * The ConnectionType this object is the Overlord of
   */
    override public ConnectionType ConnectionType
    {
      get
      {
        return ConnectionType.Unstructured;
      }
    }

    override public void Activate()
    {

      #if KML_DEBUG
        System.Console.WriteLine("In Activate for UnstructuredConnectionOverlord.");
      #endif

      if (IsActive && NeedConnection) {
      //log.Info("UnstructuredConnectionOverlord :  seeking connection");

        if ( _connection_table.Count(ConnectionType.Leaf) < 1 ) {
        //log.Warn("We need connections, but have no leaves");
          // do nothing. we must wait for a leaf node
        } else {
          if (_connection_table.Count(ConnectionType.Unstructured) < uc_bootstrap_threshold) {
            //try to get an unstructured connection by doing a forwarded connect
            //through a random leaf connection.

            //Get the leaf address we are going to use for forwarding the connection packet
            Address leaf;           
            Edge    edge;

	    lock( _connection_table.SyncRoot ) {
              int lidx = _rand.Next( _connection_table.Count(ConnectionType.Leaf) );
	      _connection_table.GetConnection(ConnectionType.Leaf,
	       		                   lidx,
                                           out leaf,
					   out edge);
	    } 

            // destination for the connect message
            RwtaAddress destination = new RwtaAddress();
            ForwardedConnectTo(leaf, destination,  unstructured_connect_ttl);
          } else {
            CheckAndConnectHandler(null, null);
          }
        }

      }

    }

  /**
   * Does the work of getting unstructured connections in response to
   * activation or connect/disconnect events.
   */
    public void CheckAndConnectHandler(object caller, EventArgs args)
    {

      #if KML_DEBUG
        System.Console.WriteLine("In CheckAndConnectHandler for UnstructuredConnectionOverlord.");
      #endif
      
      ConnectionEventArgs conargs = (ConnectionEventArgs)args;

      if ( (conargs != null) && (conargs.ConnectionType == ConnectionType.Unstructured) )  {
          if ( total_curr == total_desired ) {
            total_desired++;
          }
          total_curr++;
      }

      if (IsActive && NeedConnection) {
      //log.Info("UnstructuredConnectionOverlord :  seeking connection");

        if ( _connection_table.Count(ConnectionType.Leaf) < 1 ){
        //log.Warn("We need connections, but have no leaves");
          // do nothing. we must wait for a leaf node
        } else {
          if ( _connection_table.Count(ConnectionType.Unstructured) < 1 ) {
            Activate();
          } else {
            RwtaAddress destination = new RwtaAddress();
            ConnectTo(destination, unstructured_connect_ttl);
          }
        }
      }
    }

  /**
   * Does the work of getting unstructured connections in response to
   * activation or connect/disconnect events.
   */
    public void CheckAndDisconnectHandler(object caller, EventArgs args)
    {
      ConnectionEventArgs conargs = (ConnectionEventArgs)args;

      if ( (conargs != null) && (conargs.ConnectionType == ConnectionType.Unstructured) )
      {
        total_curr--;
        CheckAndConnectHandler(null, null);
      }
    }

    // This handles the Finish event from the connectors created in SCO.
    public void ConnectionEndHandler(object connector, EventArgs args)
    {
      lock( _sync ) {
        _connectors.Remove(connector);
      }
      //log.Info("ended connection attempt: node: " + _local.Address.ToString() );
      Activate();
    }

    protected void ForwardedConnectTo(Address forwarder,
		                      Address destination,
				      short t_ttl)
    {
      ConnectToMessage ctm = new ConnectToMessage(ConnectionType.Unstructured,
			           _local.Address, _local.LocalTAs);
      ctm.Dir = ConnectionMessage.Direction.Request;
      ctm.Id = _rand.Next(1, Int32.MaxValue);
      short t_hops = 0;
      //This is the packet we wish we could send: local -> target
      AHPacket ctm_pack = new AHPacket(t_hops,
		                       t_ttl,
				       _local.Address,
			               destination, AHPacket.Protocol.Connection,
                                       ctm.ToByteArray() );
      //We now have a packet that goes from local->forwarder, forwarder->target
      AHPacket forward_pack = PacketForwarder.WrapPacket(forwarder, 1, ctm_pack);

      #if KML_DEBUG
        System.Console.WriteLine("In UnstructuredConnectioOverlord ForwardedConnectTo:");
        System.Console.WriteLine("Local:{0}", _local.Address);
        System.Console.WriteLine("Destination:{0}", destination);    
        System.Console.WriteLine("Message ID:{0}", ctm.Id);  
      #endif

      Connector con = new Connector(_local);
      //Keep a reference to it does not go out of scope
      lock( _sync ) {
        _connectors.Add(con);
      }
      con.FinishEvent += new EventHandler(this.ConnectionEndHandler);

      con.Connect(forward_pack, ctm.Id);
    }

    protected void ConnectTo(Address destination, short t_ttl)
    {
      short t_hops = 0;
      ConnectToMessage ctm =
              new ConnectToMessage(ConnectionType.Unstructured, _local.Address,
			         _local.LocalTAs);
      ctm.Id = _rand.Next(1, Int32.MaxValue);
      ctm.Dir = ConnectionMessage.Direction.Request;

      AHPacket ctm_pack =
              new AHPacket(t_hops, t_ttl, _local.Address, destination,
                AHPacket.Protocol.Connection, ctm.ToByteArray());

      #if DEBUG
        System.Console.WriteLine("In UnstructuredConnectionOverlord ConnectTo:");
        System.Console.WriteLine("Local:{0}", _local.Address);
        System.Console.WriteLine("Destination:{0}", destination);
        System.Console.WriteLine("Message ID:{0}", ctm.Id);
      #endif

      Connector con = new Connector(_local);
      //Keep a reference to it does not go out of scope
      lock( _sync ) {
        _connectors.Add(con);
      }
      con.FinishEvent += new EventHandler(this.ConnectionEndHandler);
      con.Connect(ctm_pack, ctm.Id);
    }

  /**
   * @return true if you need a connection; an unstructured connection is needed when 
   * the number of connections is less than the total number desired OR if the total_desired
   * variable is less than two.  However, this variable should never be less than two.
   */
    override public bool NeedConnection
    {	    
      get {
        return (total_curr < total_desired || total_desired < 2);
      }
    }

  }

}

