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
    Leaf=1,                       //Connections which are point-to-point edge.
    Structured=2,                 //Connections for routing structured addresses
    Unstructured=3,               //Connections for routing unstructured addresses
    Unknown=4                     //Refers to all connections which are not in the above
  }

}
