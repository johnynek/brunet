/**
 * Dependencies
 * Brunet.ConnectionType
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

    /**
     * If IsActive, then start trying to get connections.
     */
    abstract public void Activate();
    
  /**
   * The ConnectionType this object is the Overlord of
   */
    abstract public ConnectionType ConnectionType
    {
      get;
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
  }

}
