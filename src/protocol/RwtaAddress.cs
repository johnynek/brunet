/*
 * using Brunet.UnstructuredAddress;
 */

using Brunet;

namespace Brunet
{

  /**
   * This class is a subclass of UnstructuredAddress
   * 
   */

  public class RwtaAddress:Brunet.UnstructuredAddress
  {

    ///The class of this address type
    public static readonly int _class = 159;

    public RwtaAddress() : base()
    {

    }

    public RwtaAddress(byte[] add):base(add)
    {
      if (ClassOf(add) != _class) {
        throw new System.
        ArgumentException("This is not a Class 159 address :  ",
                          this.ToString());
      }
    }

    public RwtaAddress(byte[] add, int offset):base(add, offset)
    {
      if (ClassOf(add, offset) != _class) {
        throw new System.
        ArgumentException("This is not a Class 159 address :  ",
                          this.ToString());
      }
    }

    public override int Class
    {
      get
      {
        return _class;
      }
    }

    /**
     * Message is passed to exactly one neighbor
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
