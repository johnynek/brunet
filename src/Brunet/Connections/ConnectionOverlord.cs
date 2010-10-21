/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using System;
using System.Collections;
using Brunet;
using Brunet.Util;
using Brunet.Transport;

using Brunet.Messaging;
using Brunet.Symphony;
namespace Brunet.Connections {
  /**
   * Connections are added and removed, and these classes handle
   * this.  It can tell you if a particular connection would
   * need to be replaced (if removed) and it can tell how
   * to replace this node
   */

  public abstract class ConnectionOverlord {
    protected Node _node;

    /// A TAAuthorizer for the CO
    public virtual TAAuthorizer TAAuth { get { return null;} }

    /**
     * If IsActive, then start trying to get connections.
     */
    virtual public void Activate()
    {
    }

    virtual public void Start()
    {
      IsActive = true;
      Activate();
    }

    virtual public void Stop()
    {
      IsActive = false;
    }

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

      ICollection transports = resp.Target.Transports;

      if(TAAuth != null) {
        ArrayList trans = new ArrayList();
        foreach(TransportAddress ta in resp.Target.Transports) {
          if(TAAuth.Authorize(ta) != TAAuthorizer.Decision.Deny) {
            trans.Add(ta);
          }
        }
        transports = trans;
      }

      Linker l = new Linker(_node, resp.Target.Address, transports,
          resp.ConnectionType, resp.Token);
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
      Linker l = new Linker(_node, target, null, ConnectionType, token);
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
      return new ConnectToMessage(ConnectionType, _node.GetNodeInfo(12, TAAuth), token);
    }

    virtual protected void ConnectorEndHandler(object o, EventArgs eargs) {
    }

    virtual protected void LinkerEndHandler(object o, EventArgs eargs) {
    }
  }
}
