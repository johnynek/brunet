// Brunet.Address
// Brunet.StructuredAddress;
// Brunet.BigInteger;
using System;

namespace Brunet
{

 /**
  * Represents unicast addresses which have a one to
  * one relationship with Nodes in the Brunet system.
  * Each node has at most one AHAddress, and each
  * AHAddress has at most one Node associated with
  * it.
  */

  public class AHAddress:Brunet.StructuredAddress
  {
    ///The class of this address type
    public static readonly int _class = 0;
    
    public override int Class
    {
      get
      {
        return _class;
      }
    }

    public AHAddress(byte[] add) : base(add)
    {
      if (ClassOf(add) != this.Class) {
        throw new System.
          ArgumentException("This is not an AHAddress (Class 0) :  ",
                            this.ToString());
      }
    }

    public AHAddress(byte[] add, int offset):base(add, offset)
    {
      if (ClassOf(add,offset) != this.Class) {
        throw new System.
          ArgumentException("This is not an AHAddress (Class 0) :  ",
                            this.ToString());
      }
    }

    public AHAddress(BigInteger big_int):base(big_int)
    {
      if (ClassOf(buffer) != this.Class) {
        throw new System.
          ArgumentException("This is not an AHAddress (Class 0) :  ",
                            this.ToString());
      }
    }

    /**
     * Compute the distance from this to add such that
     * the magnitude is less than or equal to Address.Half
     */
    public virtual BigInteger DistanceTo(AHAddress add)
    {
      BigInteger n_x = this.ToBigInteger();
      BigInteger n_y = add.ToBigInteger();

      BigInteger dist = n_y - n_x;
      if (dist > 0) {
        if (dist >= Address.Half) {
          dist = dist - AHAddress.Full;
        }
      }
      else {
//If dist < -Address.Half
        if (0 > (Address.Half + dist)) {
          dist = dist + AHAddress.Full;
        }
      }
      return dist;
    }

    /**
     * Use the DistanceTo function.  If this node is
     * positive distance from add it is right of.
     */
    public bool IsRightOf(AHAddress add)
    {
      return (add.DistanceTo(this) < 0);
    }

    /**
     * Use the DistanceTo function.  If this node is
     * positive distance from add, it is left of.
     */
    public bool IsLeftOf(AHAddress add)
    {
      return (add.DistanceTo(this) > 0);
    }

    /**
     * All AHAddresses are unicast
     */
    public override bool IsUnicast
    {
      get
      {
        return true;
      }
    }
  }

}

