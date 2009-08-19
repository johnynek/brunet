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
 * Dependencies
 * Brunet.ConnectionType
 */

using System;
using System.Collections;

namespace Brunet {
  /**
   * Connections are added and removed, and these classes handle
   * this.  It can tell you if a particular connection would
   * need to be replaced (if removed) and it can tell how
   * to replace this node
   */

  public abstract class ConnectionOverlord {
    protected Node _node;

    /**
     * If IsActive, then start trying to get connections.
     */
    abstract public void Activate();

    /**
     * When IsActive is false, the ConnectionOverlord does nothing
     * to replace lost connections, or to get connections it needs.
     * When IsActive is true, then any missing connections are
     * made up for
     */
    abstract public bool IsActive
    {
      get;
      set;
    }

    /**
     * @return true if the ConnectionOverlord needs a connection
     */
    abstract public bool NeedConnection
    {
      get;
    }
    /**
     * @return true if the ConnectionOverlord has sufficient connections
     *  for connectivity (no routing performance yet!)
     */
    virtual public bool IsConnected { get { throw new NotImplementedException(); } }

    /**
     * Connectors just send and receive ConnectToMessages.  They return all responses
     * to the ConnectionOverlord that initiated the ConnectToMessage
     * @return true if we have enough responses for this connector, and should
     * stop listening for more
     */
    virtual public bool HandleCtmResponse(Connector c, ISender return_path, ConnectToMessage resp) {
      /**
       * Time to start linking:
       */

      Linker l = new Linker(_node, resp.Target.Address,
                            resp.Target.Transports,
                            resp.ConnectionType,
                            _node.Address.ToString());
      l.FinishEvent += LinkerEndHandler;
      _node.TaskQueue.Enqueue( l );
      return true;
    }

    /**
     * So many ways to ConnectTo a remote node, at a minimum we need to know the
     * remote nodes address and the connection type.  Alternatively we could also
     * know a sender and a token as well.
     */
    virtual protected void ConnectTo(Address target, string ConnectionType) {
      ConnectTo(target, ConnectionType, _node.Address.ToString());
    }

    virtual protected void ConnectTo(Address target, string ConnectionType,
        string token)
    {
      ISender sender = new AHExactSender(_node, target);
      ConnectTo(sender, target, ConnectionType, _node.Address.ToString());
    }

    virtual protected void ConnectTo(ISender sender, Address target,
        string ConnectionType)
    {
      ConnectTo(sender, target, ConnectionType, _node.Address.ToString());
    }

    virtual protected void ConnectTo(ISender sender, Address target,
        string ConnectionType, string token)
    {
      Connector con = GetConnector(sender, target, ConnectionType, token);
      if(con != null) {
        _node.TaskQueue.Enqueue(con);
      }
    }

    /**
     * This method allows a user to add some state in the ConnectTo call (see SNCO).
     */
    virtual protected Connector GetConnector(ISender sender, Address target,
        string ConnectionType, string token)
    {
      ConnectionType mt = Connection.StringToMainType(ConnectionType);
      /*
       * This is an anonymous delegate which is called before
       * the Connector starts.  If it returns true, the Connector
       * will finish immediately without sending an ConnectToMessage
       */
      Linker l = new Linker(_node, target, null, ConnectionType,
          _node.Address.ToString());
      object link_task = l.Task;
      Connector.AbortCheck abort = delegate(Connector c) {
        bool stop = false;
        stop = _node.ConnectionTable.Contains( mt, target );
        if (!stop ) {
          /*
           * Make a linker to get the task.  We won't use
           * this linker.
           * No need in sending a ConnectToMessage if we
           * already have a linker going.
           */
          stop = _node.TaskQueue.HasTask( link_task );
        }
        return stop;
      };
      if (abort(null)) {
        return null;
      }

      ConnectToMessage ctm = GetConnectToMessage(ConnectionType, token);
      Connector con = new Connector(_node, sender, ctm, this, target);
      con.FinishEvent += ConnectorEndHandler;
      con.AbortIf = abort;
      return con;
    }

    /**
     * Not all COs want to have the same Ctm (see SNCO)
     */
    virtual protected ConnectToMessage GetConnectToMessage(string ConnectionType,
        string token)
    {
      return new ConnectToMessage(ConnectionType, _node.GetNodeInfo(8), token);
    }

    virtual protected void ConnectorEndHandler(object o, EventArgs eargs) {
    }

    virtual protected void LinkerEndHandler(object o, EventArgs eargs) {
    }
  }
}
