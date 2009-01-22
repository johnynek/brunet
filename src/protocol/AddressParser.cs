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

using System;
using System.Threading;

namespace Brunet
{

  /**
   * Parses ascii strings representations of an Address and returns
   * an instance of the appropriate class.
   */
  public class AddressParser
  {
    /*
     * Here is a cache so we don't have to parse the same
     * address over and over.  It is used for the string
     * representation of addresses
     */
    protected static Cache _address_cache;
    protected const int CACHE_SIZE = 128;

    //This is a specialized cache for parsing MemBlocks to Address
    protected static readonly Address[] _mb_cache;
    
    static AddressParser() {
      _address_cache = new Cache(CACHE_SIZE);
      _mb_cache = new Address[ UInt16.MaxValue + 1 ];
    }
    /** Parse without looking at the cache
     */
    protected static Address NoCacheParse(string ascii) {
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
        throw new ParseException
        ("String is not a properly formated Brunet Address:" + ascii);
      }
      string node = parts[offset + 1].ToLower();
      if (node != "node") {
        throw new ParseException
        ("String is not a properly formated Brunet Address:" + ascii);
      }
      try {
        byte[] binadd = Base32.Decode(parts[offset + 2]);
        MemBlock mb = MemBlock.Reference(binadd);
        return Parse(mb);
      }
      catch(System.ArgumentOutOfRangeException ex) {
        throw new ParseException("Failed to parse Address string",
                                 ex);
      }
    }
    /**
     * @param ascii an ascii representation of an Address
     * @return a new address of the appropriate class
     * @throw ParseException if the string is not a valid address
     * This is the inverse of the Address.ToString() function
     */
    public static Address Parse(string ascii)
    {
      Cache add_cache = Interlocked.Exchange<Cache>(ref _address_cache, null);
      //If add_cache is non-null, we have a cache to use, woohoo!
      Address a = null;
      if( add_cache != null ) {
        try {
          a = (Address)add_cache[ascii];
          if( a == null ) {
            //We need to actually do the parse:
            a = NoCacheParse(ascii);
            //Cache this result using the string reference by the address:
            add_cache[ a.ToString() ] = a;
          }
        }
        finally {
          //Make sure to always do this!!
          Interlocked.Exchange<Cache>(ref _address_cache, add_cache);
        }
      }
      else {
        //We couldn't get the cache, just go ahead, no need to wait:
        a = NoCacheParse(ascii);
      }
      return a;
    }

    /**
     * Read the address out of the buffer  This makes a copy
     * and calls Parse on the copy.  This is a "convienience" method.
     * @throw ParseException if the buffer is not a valid address
     */
    static public Address Parse(MemBlock mb)
    {
      //Read some of the least significant bytes out,
      //AHAddress all have last bit 0, so we skip the last byte which
      //will have less entropy
      ushort idx = (ushort)NumberSerializer.ReadShort(mb, Address.MemSize - 3);
      Address a = _mb_cache[idx];
      if( a != null ) {
        if( a.ToMemBlock().Equals(mb) ) {
          return a;
        }
      }
      //Else we need to read the address and put it in the cache
      try {
        if( 2 * mb.Length < mb.ReferencedBufferLength ) {
            /*
             * This MemBlock is much smaller than the array
             * we are referencing, don't keep the big one
             * in scope, instead make a copy
             */
          mb = MemBlock.Copy((ICopyable)mb);
        }
        int add_class = Address.ClassOf(mb);
        switch (add_class) {
        case 0:
          a = new AHAddress(mb);
          break;
        case 124:
          a = new DirectionalAddress(mb);
          break;
        default:
          a = null;
          throw new ParseException("Unknown Address Class: " +
                                   add_class + ", buffer:" +
                                   mb.ToString());
        }
        //Cache this result:
        _mb_cache[ idx ] = a;
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
