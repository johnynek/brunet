namespace Brunet
{

  /**
   * This is class to do base32 coversions to and from byte arrays
   */

  public class Base32
  {

    //Here is the ordered alphabet for Base32 :
    public static readonly char[]  alphabet
    = new char[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H',
                   'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P',
                   'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X',
                   'Y', 'Z', '2', '3', '4', '5', '6', '7'
                 };
    public static readonly char padding = '=';

    /**
     * @param c a character to look up its number in the base32 scheme
     * @return the number for the character A->0, B->1, etc...
     * @throws System.ArgumentOutOfRangeException if c is not valid
     */
    public static int ValueFor(char c)
    {
      //Allow lower cases :
      if (('a' <= c) && (c <= 'z')) {
        return (c - 'a');
      }
      else if (('A' <= c) && (c <= 'Z'))
      {
        return (c - 'A');
      }
      else if (('2' <= c) && (c <= '7'))
      {
        return c - '2' + 26;
      }
      //Else there has been an error
      throw new System.ArgumentOutOfRangeException("c", c,
          "Not a valid Base32 character");
    }

    /**
     * By default, we encode *WITH* padding to be standard compliant
     */
    public static string Encode(byte[] binary)
    {
      return Encode(binary, 0, binary.Length, true);
    }
    /**
     * @param binary Data to encode as base32
     * @param pad if true, add padding characters to make output length a multiple
     * of 8.
     * @return the encoded ascii string
     */
    public static string Encode(byte[] binary, bool pad)
    {
      return Encode(binary, 0, binary.Length, pad);
    }
    public static string Encode(byte[] binary, int offset, int length,
                                bool pad)
    {

      int enc_length = 8 * (length / 5);
      int pad_length = 0;
      switch (length % 5) {
      case 1:
        pad_length = 6;
        break;
      case 2:
        pad_length = 4;
        break;
      case 3:
        pad_length = 3;
        break;
      case 4:
        pad_length = 1;
        break;
      default:
        pad_length = 0;
        break;
      }
      if (pad_length > 0) {
        if (pad) {
          //Add a full block
          enc_length += 8;
        }
        else {
          //Just add the chars we need :
          enc_length += (8 - pad_length);
        }
      }
      char[]  encoded = new char[enc_length];
      //Here are all the full blocks :
      //This is the number of full blocks :
      int blocks = length / 5;
      for (int block = 0; block < blocks; block++) {
        EncodeBlock(encoded, 8 * block, binary, offset + 5 * block,
                    5);
      }
      //Here is one last partial block
      EncodeBlock(encoded, 8 * blocks, binary, offset + 5 * blocks,
                  length % 5);
      //Add the padding at the end
      if (pad) {
        for (int i = 0; i < pad_length; i++) {
          encoded[enc_length - i - 1] = padding;
        }
      }
      return new System.String(encoded);
    }

    protected static void EncodeBlock(char[] ascii_out,
                                      int ascii_off,
                                      byte[] bin,
                                      int offset, int block_length)
    {

      //The easiest thing is just to do this by hand :
      int idx = 0;
      switch (block_length) {
      case 5:
        idx |= bin[offset + 4] & 0x1F;
        ascii_out[ascii_off + 7] = alphabet[idx];
        idx = 0;
        idx |= (bin[offset + 4] & 0xE0) >> 5;
      goto case 4;
      case 4:
        idx |= (bin[offset + 3] & 0x03) << 3;
        ascii_out[ascii_off + 6] = alphabet[idx];
        idx = 0;
        idx |= (bin[offset + 3] & 0x7C) >> 2;
        ascii_out[ascii_off + 5] = alphabet[idx];
        idx = 0;
        idx |= (bin[offset + 3] & 0x80) >> 7;
      goto case 3;
      case 3:
        idx |= (bin[offset + 2] & 0x0F) << 1;
        ascii_out[ascii_off + 4] = alphabet[idx];
        idx = 0;
        idx |= (bin[offset + 2] & 0xF0) >> 4;
      goto case 2;
      case 2:
        idx |= (bin[offset + 1] & 0x01) << 4;
        ascii_out[ascii_off + 3] = alphabet[idx];
        idx = 0;
        idx |= (bin[offset + 1] & 0x3E) >> 1;
        ascii_out[ascii_off + 2] = alphabet[idx];
        idx = 0;
        idx |= (bin[offset + 1] & 0xC0) >> 6;
      goto case 1;
      case 1:
        idx |= (bin[offset] & 0x7) << 2;
        ascii_out[ascii_off + 1] = alphabet[idx];
        idx = 0;
        idx |= bin[offset] >> 3;
        ascii_out[ascii_off] = alphabet[idx];
        break;
      default:
        break;
      }
    }

    /**
     * @param ascii the string containing the base32 data (ONLY)
     * @return decoded binary
     * @throws System.ArgumentOutOfRangeException if there are
     * any characters in the ascii that are not valid base32 chars before
     * the first padding character :  '=' All characters after the first
     * padding character are ignored.
     */
    public static byte[]  Decode(string ascii)
    {
      //Now find the number of non-pad chars in the last block
      int pad_pos = ascii.IndexOf(padding);
      if (pad_pos == -1) {
        //consider the end of the string to be the position of the '='
        pad_pos = ascii.Length;
      }
      //These are full blocks :
      int data_length = 5 * (pad_pos / 8);
      switch (pad_pos % 8) {
      case 7:
        data_length += 4;
        break;
      case 6:
      goto case 5;
      case 5:
        data_length += 3;
        break;
      case 4:
      goto case 3;
      case 3:
        data_length += 2;
        break;
      case 2:
        data_length += 1;
        break;
      case 1:
      goto default;
      default:
        break;
      }
      byte[] binary = new byte[data_length];
      int blocks = pad_pos / 8;
      for (int block = 0; block < blocks; block++) {
        DecodeBlock(binary, 5 * block, ascii, 8 * block, 8);
      }
      DecodeBlock(binary, 5 * blocks, ascii, 8 * blocks, pad_pos % 8);
      return binary;
    }

    protected static void DecodeBlock(byte[] data, int offset,
                                      string ascii, int a_off,
                                      int encl)
    {
      //We just do this by hand :
      int val = 0;
      switch (encl) {
      case 8:
        val |= ValueFor(ascii[a_off + 7]);
        val |= (ValueFor(ascii[a_off + 6]) & 0x7) << 5;
        data[offset + 4] = (byte) val;
        val = 0;
      goto case 7;
      case 7:
        val |= ValueFor(ascii[a_off + 6]) >> 3;
        val |= ValueFor(ascii[a_off + 5]) << 2;
        val |= (ValueFor(ascii[a_off + 4]) & 1) << 7;
        data[offset + 3] = (byte) val;
        val = 0;
      goto case 5;
      case 6:
      goto case 5;
      case 5:
        val |= ValueFor(ascii[a_off + 4]) >> 1;
        val |= (ValueFor(ascii[a_off + 3]) & 0xF) << 4;
        data[offset + 2] = (byte) val;
        val = 0;
      goto case 4;
      case 4:
        val |= ValueFor(ascii[a_off + 3]) >> 4;
        val |= ValueFor(ascii[a_off + 2]) << 1;
        val |= (ValueFor(ascii[a_off + 1]) & 0x03) << 6;
        data[offset + 1] = (byte) val;
        val = 0;
      goto case 2;
      case 3:
      goto case 2;
      case 2:
        val |= ValueFor(ascii[a_off + 1]) >> 2;
        val |= ValueFor(ascii[a_off]) << 3;
        data[offset] = (byte) val;
      goto case 1;
      case 1:
      goto default;
      default:
        break;
      }
    }

    public static void Test()
    {
      System.Console.WriteLine("Testing Base32 : ");

      byte[] test = new byte[] {
                      0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16};
      System.Console.WriteLine("Encoded :  {0}", Encode(test, false));

      for (int j = 1; j < 1000; j++) {
        test = new byte[j];
        for (int k = 0; k < test.Length; k++) {
          test[k] = (byte) (k ^ j + j * k);
        }
        //Test if encoding and decoding is transparant :
        byte[] test2 = Decode(Encode(test, true));
        for (int i = 0; i < test.Length; i++) {
          if (test2[i] != test[i]) {
            System.Console.
            WriteLine("Index :  {0} not equal, {1} != {2}", i,
                      test[i], test2[i]);
          }
          if (test2.Length != test.Length) {
            System.Console.
            WriteLine("Decoded length not equal to original!");
          }
        }
      }
    }
    public static void Main(string[] args)
    {
      foreach(string a in args)
      {
        System.Console.WriteLine("{0}:", a);
        byte[] data = Decode(a);
        foreach(byte datum in data){
          System.Console.Write("{0}:", datum);
        }
        System.Console.WriteLine("\n--");
      }
    }
  }


}
