// Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida
// For license, see the file LICENSE in the root directory of this software.

using System;
using System.Text;

using Brunet.Connections;
using Brunet.Transport;

namespace Brunet.Simulator.Tasks {
  public class AreConnected: Task {
    protected Node _node0;
    protected Node _node1;
    protected bool _node0_done;
    protected bool _node1_done;
    protected TransportAddress.TAType _type;
    public TransportAddress.TAType TAType { get { return _type; } }
    public string TATypeAsString { get { return TransportAddress.TATypeToString(_type); } }

    public AreConnected(Node node0, Node node1, EventHandler finished) :
      base(finished)
    {
      _node0 = node0;
      _node1 = node1;
      _node0.ConnectionTable.ConnectionEvent += HandleConnection;
      _node1.ConnectionTable.ConnectionEvent += HandleConnection;
      _node0_done = false;
      _node1_done = false;
    }

    override public void Start()
    {
      base.Start();
      Address addr0 = _node0.Address, addr1 = _node1.Address;
      _node0_done = _node0.ConnectionTable.GetConnection(ConnectionType.Structured, addr1) != null;
      _node1_done = _node1.ConnectionTable.GetConnection(ConnectionType.Structured, addr0) != null;
      if(_node0_done && _node1_done) {
        Finished();
      }
    }

    protected void HandleConnection(object sender, EventArgs ea)
    {
      ConnectionEventArgs cea = ea as ConnectionEventArgs;
      if(cea.ConnectionType != ConnectionType.Structured) {
        return;
      }

      ConnectionTable ct = sender as ConnectionTable;
      if(_node0.ConnectionTable == ct) {
        _node0_done |= _node1.Address == cea.RemoteAddress;
      } else if(_node1.ConnectionTable == ct) {
        _node1_done |= _node0.Address == cea.RemoteAddress;
      }

      if(_node0_done && _node1_done) {
        Finished();
      }
    }

    override protected void Finished()
    {
      _node0.ConnectionTable.ConnectionEvent -= HandleConnection;
      _node1.ConnectionTable.ConnectionEvent -= HandleConnection;
      Connection con = _node0.ConnectionTable.GetConnection(ConnectionType.Structured, _node1.Address);
      if(con != null) {
        _type = con.State.Edge.TAType;
      }
      base.Finished();
    }

    override public string ToString()
    {
      StringBuilder sb = new StringBuilder();
      sb.Append("AreConnected State: ");
      if(!Done) {
        sb.Append("In progress.");
        return sb.ToString();
      }

      sb.Append("Complete");
      sb.Append("\n\bTook: " + _time_taken);
      sb.Append("\n\bEdge type: " + _type);
      return sb.ToString();
    }
  }
}
