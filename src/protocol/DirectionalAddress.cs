/**
 * Dependencies
 * Brunet.Address
 * Brunet.StructuredAddress
 * Brunet.NumberSerializer
 */
namespace Brunet
{

  /**
   * This address represents directional routing
   * in the structured system, which is class 124
   */

  public class DirectionalAddress:StructuredAddress
  {

    public DirectionalAddress(byte[] binadd, int offset)
    {
      if (ClassOf(binadd, offset) != this.Class) {
        throw new System.
        ArgumentException
        ("This is not an AHAddress (Class 124) :  ",
         this.ToString());
      }
      _dir = (Direction) NumberSerializer.ReadInt(binadd, offset);

      buffer = new byte[Address.MemSize];
      SetClass(this.Class);
      NumberSerializer.WriteInt((int)_dir, buffer, 0);
    }

    public DirectionalAddress(byte[] binadd)
    {
      if (ClassOf(binadd) != this.Class) {
        throw new System.
        ArgumentException
        ("This is not an AHAddress (Class 124) :  ",
         this.ToString());
      }
      //The first 32 bits are an integer which refers to the
      //direction
      _dir = (Direction) NumberSerializer.ReadInt(binadd, 0);

      buffer = new byte[Address.MemSize];
      SetClass(this.Class);
      NumberSerializer.WriteInt((int)_dir, buffer, 0);
    }

    public DirectionalAddress(DirectionalAddress.Direction bearing) : base()
    {
      NumberSerializer.WriteInt((int)bearing, buffer, 0);
      _dir = bearing;
    }

    /**
     * This is class 124
     */
    public override int Class
    {
      get
      {
        return 124;
      }
    }

    /**
     * This class is always unicast
     */
    public override bool IsUnicast
    {
      get
      {
        return true;
      }
    }

    /**
     * These are defined in the spec
     */
  public enum Direction:int
    {
      ///Left is "clockwise", or increasing in numerical address
      Left = 1,
      ///Right is "counterclockwise", or decreasing
      Right = 2
    }

    protected Direction _dir;
    /**
     * Use the synonym "Bearing" for the Direction property
     */
    public Direction Bearing
    {
      get
      {
        return _dir;
      }
    }
  }

}
