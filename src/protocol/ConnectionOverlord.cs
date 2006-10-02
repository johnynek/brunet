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
 * Brunet.BrunetLogger
 */

using System.Collections;
//using log4net;

namespace Brunet
{

  /**
   * Connections are added and removed, and these classes handle
   * this.  It can tell you if a particular connection would
   * need to be replaced (if removed) and it can tell how
   * to replace this node
   */

  public abstract class ConnectionOverlord
  {
    /*private static readonly log4net.ILog log =
      log4net.LogManager.GetLogger(System.Reflection.MethodBase.
      GetCurrentMethod().DeclaringType);*/
#if PLAB_LOG
    private BrunetLogger _logger;
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
    abstract public bool IsConnected
    {
      get;
    }    
    /**
     * Connectors just send and receive ConnectToMessages.  They return all responses
     * to the ConnectionOverlord that initiated the ConnectToMessage
     */
    virtual public void HandleCtmResponse(Connector c, AHPacket resp_p, ConnectToMessage resp) {
    
    }
  }

}
