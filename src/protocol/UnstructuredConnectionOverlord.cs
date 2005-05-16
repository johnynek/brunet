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

//#define KML_DEBUG

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

    //the total number of desired connections
    //the minimum number is two and this should be a non-decreasing number
    protected int total_desired = 2;

    //this variable is for keeping track of the
    //total number of unstructured connections
    protected int total_curr = 0;

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
    override public void Activate()
    {

      #if KML_DEBUG
      System.Console.WriteLine("In Activate for UnstructuredConnectionOverlord.");
      #endif

      bool try_now = false;
      lock( _sync ) {
        if( _connectors.Count == 0 && IsActive && NeedConnection ) {
          /**
	   * We only try one connector at a time.  When that connector finishes
	   * we will check to see if we should try again.  This prevents us from
	   * building up too many connections too quickly.
	   */
          try_now = true;
        }
      }
      if ( try_now ) {
        Connection tmp_leaf = _connection_table.GetRandom(ConnectionType.Leaf);
        if( tmp_leaf == null ) {
          /*
           * We don't have any leafs to forward with
           */
        }
        else {
          /*
           * Make a random walk to find a new node.
           * We always forward through a leaf because
           * we want to start from an approximately random
           * point on the network.
           */
          RwtaAddress destination = new RwtaAddress();
          ForwardedConnectTo(tmp_leaf.Address, destination,
                             unstructured_connect_ttl);
          
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
      lock( _sync ) {
        if ( (conargs != null) &&
             (conargs.ConnectionType == ConnectionType.Unstructured) )  {
          if ( total_curr == total_desired ) {
            total_desired++;
	    /**
	     * To make the degree distribution close to 1/k^2, with probability
	     * p we reactively get a new connection.  Each new connection attempt
	     * will create 1/(1-p) edges, so p should not be too close to 1.
	     */
	    if( _rand.NextDouble() < 0.5 ) {
              total_desired++;
	    }
          }
          total_curr++;
        }
      }
      Activate();
      
    }

    /**
     * Does the work of getting unstructured connections in response to
     * activation or connect/disconnect events.
     */
    public void CheckAndDisconnectHandler(object caller, EventArgs args)
    {
      ConnectionEventArgs conargs = (ConnectionEventArgs)args;

      if ( (conargs != null) &&
           (conargs.ConnectionType == ConnectionType.Unstructured) )
      {
        lock( _sync ) {
          total_curr--;
        }
      }
      Activate();
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


    /**
     * This method of getting unstructured connections is depracated.
     * ALL Unstructured connections are obtained by forwarding through
     * a randomly selected leaf connection.
     */
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
        lock( _sync ) {
          return (total_curr < total_desired || total_desired < 2);
        }
      }
    }

  }

}

