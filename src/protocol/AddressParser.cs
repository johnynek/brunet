/**
 * Dependencies
 * Brunet.Address
 * Brunet.AHAddress
 * Brunet.Base32
 * Brunet.DirectionalAddress
 * Brunet.ParseException
 * Brunet.RwpAddress
 * Brunet.RwtaAddress
 */

using System;

namespace Brunet
{

/**
 * Parses ascii strings representations of an Address and returns
 * an instance of the appropriate class.
 */
  public class AddressParser
  {

  /**
   * @param ascii an ascii representation of an Address
   * @return a new address of the appropriate class
   * @throw ParseException if the string is not a valid address
   * This is the inverse of the Address.ToString() function
   */
    public static Address Parse(string ascii)
    {

      string[] parts = ascii.Split(':');
//It should be:  urn:brunet:node:[encoded address]
// or brunet:node:[encoded address]
      int offset = 0;
      if (parts[0].ToLower() == "urn") {
        offset = 1;
      }
      string brunet = parts[offset].ToLower();
      if (brunet != "brunet")
      {
        throw new
          ParseException
          ("String is not a properly formated Brunet Address:" +
           ascii);
      }
      string node = parts[offset + 1].ToLower();
      if (node != "node") {
        throw new
          ParseException
          ("String is not a properly formated Brunet Address:" +
           ascii);
      }
      try {
        byte[] binadd = Base32.Decode(parts[offset + 2]);
        return Parse(binadd);
      }
      catch(System.ArgumentOutOfRangeException ex) {
        throw new ParseException("Failed to parse Address string",
                                 ex);
      }
    }

  /**
   * Read the address out of the buffer  This makes a copy
   * and calls Parse on the copy.  This is a "convienience" method.
   * @throw ParseException if the buffer is not a valid address
   */
    static public Address Parse(byte[] binadd, int offset)
    {
      try {
        int add_class = Address.ClassOf(binadd, offset);
        Address a = null;
        switch (add_class) {
        case 0:
          a = new AHAddress(binadd, offset);
          break;
	case 124:
	  a = new DirectionalAddress(binadd, offset);
	  break;
        case 126:
          a = new RwpAddress(binadd, offset);
          break;
        case 159:
          a = new RwtaAddress(binadd, offset);
          break;
        default:
          throw new ParseException("Unknown Address Class: " +
                                   add_class + ", buffer:" +
                                   binadd.ToString() + " offset: " +
                                   offset);
        }
        return a;
      }
      catch(ArgumentOutOfRangeException ex) {
        throw new ParseException("Address too short: " +
                                 binadd.ToString() + "offset: " +
                                 offset, ex);
      }
      catch(ArgumentException ex) {
        throw new ParseException("Could not parse: " +
                                 binadd.ToString() + "offset: " +
                                 offset, ex);
      }
    }

  /**
   * Given the binadd, return an Address object which this
   * binary representation corresponds to.  The buffer must
   * be exactly Address.MemSize long, or an exception is thrown.
   * @throw ParseException if the buffer is not a valid address
   */
    static public Address Parse(byte[] binadd)
    {
      return Parse(binadd, 0);
    }
  }

}
