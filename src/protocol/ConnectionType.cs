/**
 * Simple Enum to signify which edges are what types
 */

namespace Brunet
{

  /**
   * These are the various connection types.  The order
   * of the enum goes from most important to least important.
   * The connections should be added in this order.
   */
  public enum ConnectionType
  {
    Leaf,                       //Connections which are point-to-point edge.
    Structured,                 //Connections for routing structured addresses
    StructuredNear,             //Near neighbor connections
    StructuredShortcut,         //shortcut connection
    Unstructured,               //Connections for routing unstructured addresses
    Unknown                     //Refers to all connections which are not in the above
  }

}
