/**
 * Dependencies : 
 * Brunet.Address
 * Brunet.AHAddress;
 * Brunet.BigInteger
 */

namespace Brunet
{

 /**
  * The Brunet system routes messages by sending them
  * towards a Connection which has the closest Address.
  * This comparer does address comparing for use in .Net
  * framework classes which use the IComparer interface.
  */

  public class AHAddressComparer:System.Collections.IComparer
  {
    protected AHAddress _zero;
    /**
     * Zero is where we will count zero from.  Half Is half way point
     */
    public AHAddress Zero { get { return _zero; } }
    public AHAddressComparer()
    {
      _zero = new AHAddress(Address.Half.getBytes());
    }

    /**
     * @param zero the address to use as the zero in the space
     */
    public AHAddressComparer(Address zero)
    {
      byte[] binzero = new byte[Address.MemSize];
      zero.CopyTo(binzero);
//Make sure the last bit is zero, so the address is class 0
      binzero[Address.MemSize - 1] &= 0xFE;
      _zero = new AHAddress(binzero);
    }

    public int Compare(object x, object y)
    {
//Equals is fast to check, lets do it before we
//do more intense stuff : 
      if (x.Equals(y)) {
        return 0;
      }

      if ((x is AHAddress) && (y is AHAddress)) {
        AHAddress add_x = (AHAddress) x;
        AHAddress add_y = (AHAddress) y;
      /**
       * We compute the distances with the given zero
       * n_x - n_y = (n_x - n_z) - (n_y - n_z);
       *
       * The AHAddress.DistanceTo function gives
       * the distance as measured from the node.
       *
       * We can use this to set the "zero" we want : 
       */
        BigInteger dist_x = _zero.DistanceTo(add_x);
        BigInteger dist_y = _zero.DistanceTo(add_y);
//Since we know they are not equal, either n_x is bigger
//that n_y or vice-versa : 
        if (dist_x > dist_y) {
  //Then dist_x - dist_y > 0, and n_x is the bigger
          return 1;
        }
        else {
          return -1;
        }
      }
      else {
        /**
   * Just to make sure we can compare any type of address : 
   */
        BigInteger bi_x = ((Address) x).ToBigInteger();
        BigInteger bi_y = ((Address) y).ToBigInteger();
        if (bi_x > bi_y) {
          return 1;
        }
        else {
          return -1;
        }
      }
    }

  }

}
