/*
 * using Brunet.Address;
 */

using Brunet;

namespace Brunet
{

  /**
   * All addresses which are used as aliases for
   * routing rules on the unstructured system
   * have addresses which are subclasses of this
   * address.
   */

  abstract public class UnstructuredAddress:Brunet.Address
  {
    public UnstructuredAddress() : base()
    {

    }
    public UnstructuredAddress(byte[] add):base(add)
    {

    }
    public UnstructuredAddress(byte[] add, int offset):base(add,
              offset)
    {

    }
  }

}
