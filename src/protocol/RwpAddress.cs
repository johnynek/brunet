/*
 * using Brunet.UnstructuredAddress;
 * using Brunet.NumberSerializer;
 */

namespace Brunet
{

  /**
   * This class is a subclass of UnstructuredAddress
   * 
   */

  public class RwpAddress:Brunet.UnstructuredAddress
  {
    ///The class of this address:
    public static readonly int _class = 126;

    public RwpAddress(byte[] add):base(add)
    {
      if (ClassOf(add) != _class) {
        throw new System.
        ArgumentException("This is not a Class 126 address :  ",
                          this.ToString());
      }
    }
    public RwpAddress(byte[] add, int offset):base(add, offset)
    {
      if (ClassOf(add,offset) != _class) {
        throw new System.
        ArgumentException("This is not a Class 126 address :  ",
                          this.ToString());
      }
    }
    public RwpAddress(bool flag, float p) : base() {
      NumberSerializer.WriteFlag(flag, buffer, 4);
      NumberSerializer.WriteFloat(p, buffer, 0);
    }

    public override int Class
    {
      get
      {
        return _class;
      }
    }
    public bool Flag
    {
      get
      {
        return NumberSerializer.ReadFlag(buffer, 4);
      }
    }

    /**
     * This address is only unicast in the case that the percolation
     * probability is exactly zero.
     */
    public override bool IsUnicast
    {
      get
      {
        return (this.Prob == 0.0);
      }
    }

    public float Prob
    {
      get
      {
        return Brunet.NumberSerializer.ReadFloat(buffer, 0);
        //The probability is stored in the first 4 bytes of the buffer
      }
    }
  }

}
