/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

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
        MemBlock mb =MemBlock.Reference(binadd, 0, binadd.Length);
        return Parse(mb);
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
    static public Address Parse(MemBlock mb)
    {
      try {
        int add_class = Address.ClassOf(mb);
        Address a = null;
        switch (add_class) {
        case 0:
          a = new AHAddress(mb);
          break;
        case 124:
          a = new DirectionalAddress(mb);
          break;
        case 126:
          a = new RwpAddress(mb);
          break;
        case 159:
          a = new RwtaAddress(mb);
          break;
        default:
          throw new ParseException("Unknown Address Class: " +
                                   add_class + ", buffer:" +
                                   mb.ToString());
        }
        return a;
      }
      catch(ArgumentOutOfRangeException ex) {
        throw new ParseException("Address too short: " +
                                 mb.ToString(), ex);
      }
      catch(ArgumentException ex) {
        throw new ParseException("Could not parse: " +
                                 mb.ToString(), ex);
      }
    }
  }

}
