/*
 * Brunet.Address;
 * Brunet.BigInteger;
 */

namespace Brunet
{

 /**
  * All addresses which are used as aliases for
  * routing rules on the unstructured system
  * have addresses which are subclasses of this
  * address.
  */

  abstract public class StructuredAddress:Brunet.Address
  {

    public StructuredAddress() : base()
    {

    }
    public StructuredAddress(byte[] add):base(add)
    {

    }
    public StructuredAddress(byte[] add, int offset):base(add, offset)
    {

    }
    public StructuredAddress(BigInteger big_int):base(big_int)
    {
    }

  }

}


