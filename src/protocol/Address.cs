// Brunet.BigInteger;
// Brunet.Base32;
// Brunet.NumberSerializer


namespace Brunet
{

  /**
   * Represents addresses on the Brunet system.  The Brunet
   * system is a virtual network onto of lower layers (such
   * as IP).  The Address represents addresses on 
   * the virtual system.
   *
   * All Address subclasses are immutable.  Once created,
   * they cannot be changed
   */

  public abstract class Address
  {

    ///The number of bytes to represent the address
    public static readonly int MemSize = 20;

    /**
     * Static constructor initializes _half and _full
     */
    static Address()
    {
      //Initialize _half
      byte[] tmp = new byte[MemSize];
      for (int i = 1; i < MemSize; i++)
      {
        tmp[i] = 0;
      }
      //Set the first bit to 1, all else to zero :
      tmp[0] = 0x80;
      _half = new BigInteger(tmp);
      _full = _half * 2;
    }

    public Address()
    {
      buffer = new byte[MemSize];
      SetClass(this.Class);
    }

    /**
     * Create an address from a buffer with an offset
     */
    public Address(byte[] binary, int offset)
    {
      buffer = new byte[MemSize];

      /**
       * @throw ArgumentNullException if sourceArray or destinationArray
       * is null.
       * @throw RankException if sourceArray and destinationArray have 
       * different ranks. 
       * @throw ArgumentOutOfRangeException.
       */

      System.Array.Copy(binary, offset, buffer, 0, MemSize);
    }

    public Address(byte[] binary)
    {
      buffer = new byte[MemSize];
      /**
       * @throw ArgumentNullException if sourceArray or destinationArray
       * is null.
       * @throw RankException if sourceArray and destinationArray have 
       * different ranks.
       * @throw ArgumentOutOfRangeException.
       */

      System.Array.Copy(binary, 0, buffer, 0, MemSize);
    }

    public Address(BigInteger big_int)
    {
      this.Set(big_int);
    }

    abstract public int Class
    {
      get;
      }

      protected byte[]  buffer;

    public static int ClassOf(byte[] binary_add)
    {
      return ClassOf(binary_add, 0);
    }
    public static int ClassOf(byte[] binary_add, int offset)
    {
      int c = 0;
      int i = MemSize + offset - 1;
      do {
        uint t = binary_add[i];
        int shifts = 0;
        while (((t & 0x01) == 0x01) && (shifts < 8)) {
          t >>= 1;
          shifts++;
          c++;
        }
        i--;

      } while (c % 8 == 0 && c >= 8 && c <= (8 * MemSize) && i >= 0);
      /**
       * One would need to go to the next byte only if the first
       * zero has not been found yet A do-while loop is the most
       * appropriate here since we don't have to go through
       * all the bytes.  
       */
      return c;
    }

    public override bool Equals(object a)
    {
      Address add = a as Address;
      if (add != null) {
        byte[] buf_x = buffer;
        byte[] buf_y = add.buffer;

        bool equal = true;
        int i = 0;
        while (equal && (i < MemSize)) {
          equal = (buf_x[i] == buf_y[i]);
          i++;
        }
        return equal;
      }
      else {
        return false;
      }
    }

    public override int GetHashCode()
    {
      int ArrLength = MemSize / 4;
      //MemSize is the number of bytes and there are four bytes to an int
      int hash = 0;
      for (int i = 0; i < ArrLength; i++) {
        hash ^= NumberSerializer.ReadInt(buffer, i * 4);
      }
      return hash;
    }

    protected static BigInteger _half;
    /**
     * Half is 2^159
     */
    public static BigInteger Half
    {
      get
      {
        return _half;
      }
    }
    protected static BigInteger _full;
    /**
     * Full is 2 * Half = 2^160
     */
    public static BigInteger Full
    {
      get
      {
        return _full;
      }
    }

    /**
     * If this address is unicast (can only be passed to one Node)
     * this is true.  If it may be passed to more than one Node
     * it is false.
     */
    public abstract bool IsUnicast
    {
      get;
      }

      /**
       * Copy the buffer out
       */
      public virtual void CopyTo(byte[] b, int offset)
    {
      /**
       * @throw ArgumentNullException if sourceArray or destinationArray
       * is null.
       * @throw RankException if sourceArray and destinationArray have 
       * different ranks.
       * @throw ArgumentOutOfRangeException.
       */

      System.Array.Copy(buffer, 0, b, offset, MemSize);
    }

    public virtual void CopyTo(byte[] b)
    {
      CopyTo(b, 0);
    }

    public virtual BigInteger ToBigInteger()
    {
      return new BigInteger(buffer);
    }

    /**
     * Set the address from a BigInteger
     * @throw System.ArgumentException if the BigInteger is not
     * the same address class as the object.
     */
    protected void Set(BigInteger value)
    {

      byte[] bi_buf;
      if( value < 0 ) {
        //if we are less than 0, get 2^160 + value:
        BigInteger value_plus = new BigInteger(value + Full);
        bi_buf = value_plus.getBytes();
      }
      else {
        bi_buf = value.getBytes();
      }
      if (bi_buf.Length < MemSize) {
        //Missing some bytes at the beginning, pad with zero :
        byte[] tmp_bi = new byte[Address.MemSize];
        int missing = (MemSize - bi_buf.Length);
        for (int i = 0; i < missing; i++) {
          tmp_bi[i] = (byte) 0;
        }
        /**
         * @todo throw an ArgumentNullException if sourceArray or destinationArray
         * is null.Throw RankException if sourceArray and destinationArray have 
         * different ranks. Throw ArgumentOutOfRangeException.
         */
        System.Array.Copy(bi_buf, 0, tmp_bi, missing,
                          bi_buf.Length);
        bi_buf = tmp_bi;
      }
      else if (bi_buf.Length > MemSize) {
        throw new System.ArgumentException(
          "Integer too large to fit in 160 bits: " + value.ToString());
      }

      if( ClassOf(bi_buf) == this.Class ) {
        buffer = bi_buf;
      }
      else {
        throw new System.
        ArgumentException("Cannot set to a different address class,  " +
                          this.ToString() + " is class " + this.Class
                          + " not class " + ClassOf(buffer));
      }
    }

    /**
     * Sets the last bits of the address
     * to guarantee it is of a given class.
     * Used by subclasses for initialization
     */
    protected void SetClass(int myclass)
    {
      int c = 0;
      int i = MemSize - 1;
      //Set the last bit to zero:
      buffer[i] &= 0xFE;
      do {
        int shifts = 0;
        uint val = 0x01;
        while ((myclass > 0) && (shifts < 8)) {
          //Set the bit to 1:
          buffer[i] = (byte)(buffer[i] | val);
          val <<= 1;
          shifts++;
          myclass--;
        }
        i--;

      } while ( (myclass > 0) && (i >= 0) );
    }

    /**
     * @return an array of integers which represent the address
     */
    public virtual int[] ToIntArray()
    {
      int ArrLength = MemSize / 4;
      int[] int_array = new int[ArrLength];
      //MemSize is the number of bytes and there are four bytes to an int
      for (int i = 0; i < ArrLength; i++) {
        int_array[i] = NumberSerializer.ReadInt(buffer, i * 4);
      }
      return int_array;
    }
    public override string ToString()
    {
      return ("brunet:" + "node:" + Base32.Encode(buffer));
    }
  }

}



