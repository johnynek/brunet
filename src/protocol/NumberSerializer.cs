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
using System.IO;
using System.Net;
using System.Text;

namespace Brunet
{

  /**
   * Reads numbers in and out byte arrays
   */
  public class NumberSerializer
  {

    /**
     * When we are serializing bytes this is the length we need
     */
    public static int GetByteCount(string s)
    {
      //We just need one more byte than the UTF8 encoding does
      return Encoding.UTF8.GetByteCount(s) + 1; 
    }
    public static bool ReadBool(Stream s)
    {
      int val = s.ReadByte();
      if( val < 0 ) {
        throw new Exception("Reached EOF");
      }
      //If the value is 0, false, otherwise true 
      return (val > 0);
    }
    /**
     * Reads a network endian (MSB) from bin
     */
    public static int ReadInt(byte[] bin, int offset)
    {
      int net_val = BitConverter.ToInt32(bin, offset);
      int retval = IPAddress.NetworkToHostOrder(net_val);
      return retval;
    }
    /**
     * Read an Int from the stream and advance the stream
     */
    public static int ReadInt(System.IO.Stream s) {
      int bytes = 4;
      int val = 0;
      int tmp;
      while( bytes-- > 0 ) {
        tmp = s.ReadByte();
	if ( tmp == -1 ) {
          throw new Exception("Could not read 4 bytes from the stream to read an int");
        }
        val = (val << 8) | tmp;
      }
      return val;
    }
    /**
     * Read a Long from the stream and advance the stream
     */
    public static long ReadLong(System.IO.Stream s) {
      int bytes = 8;
      long val = 0;
      int tmp;
      while( bytes-- > 0 ) {
        tmp = s.ReadByte();
	if ( tmp == -1 ) {
          throw new Exception("Could not read 8 bytes from the stream to read a long");
        }
        val = (val << 8) | tmp;
      }
      return val;
    }

    public static long ReadLong(byte[] bin, int offset)
    {
      long l_val = BitConverter.ToInt64(bin, offset);
      return IPAddress.NetworkToHostOrder(l_val);
    }

    public static short ReadShort(byte[] bin, int offset)
    {
      short net_val = BitConverter.ToInt16(bin, offset);
      short retval = IPAddress.NetworkToHostOrder(net_val);
      return retval;
    }

    /**
     * Read a short from the stream and advance the stream
     */
    public static short ReadShort(System.IO.Stream s) {
      int result = s.ReadByte();
      if ( result == -1 ) {
        throw new Exception("Could not read 2 bytes from the stream to read a short");
      }
      short ret_val = (short)(result << 8);
      result = s.ReadByte();
      if ( result == -1 ) {
        throw new Exception("Could not read 2 bytes from the stream to read a short");
      }
      ret_val |= (short)result;
      return ret_val;
    }

    /**
     * This method reads UTF-8 strings out of byte arrays by looking
     * for the string up to the first zero byte.
     * 
     * While strings are not numbers, this serialization code
     * is put here anyway.
     *
     * @param bin the byte array
     * @param offset where to start looking.
     * @param bytelength how many bytes did we ready out
     *
     */
    public static string ReadString(byte[] bin, int offset, out int bytelength)
    {
      //Find the end of the string:
      int string_end = offset;
      while( bin[string_end] != 0 ) {
        string_end++;
      }
      //Add 1 for the null terminator
      bytelength = string_end - offset + 1;
      Encoding e = Encoding.UTF8;
      //subtract 1 for the null terminator
      return e.GetString(bin, offset, bytelength - 1); 
    }
    /**
     * Read a UTF8 string from the stream
     * @param s the stream to read from
     * @param count the number of bytes we read from the stream.
     */
    public static string ReadString(System.IO.Stream s, out int len)
    {
      bool cont = true; 
      //Here is the initial buffer we make for reading the string:
      byte[] str_buf = new byte[32];
      int pos = 0;
      do {
        int val = s.ReadByte();
        if( val == 0 ) {
          //This is the end of the string.
          cont = false;
        }
        else if( val < 0 ) {
          //Some kind of error occured
          string str = Encoding.UTF8.GetString(str_buf, 0, pos);
          throw new Exception("Could not read the next byte from stream, string so far: " + str);
        }
        else {
          str_buf[pos] = (byte)val;
          pos++;
          if( str_buf.Length <= pos ) {
            //We can't fit anymore into this buffer.
            //Make a new buffer twice as long
            byte[] tmp_buf = new byte[ str_buf.Length * 2 ];
            Array.Copy(str_buf, 0, tmp_buf, 0, str_buf.Length);
            str_buf = tmp_buf;
          }
        }
      } while( cont == true );
      len = pos + 1; //1 byte for the null
      return Encoding.UTF8.GetString(str_buf, 0, pos);
    }
    
    public static float ReadFloat(byte[] bin, int offset)
    {
      if (BitConverter.IsLittleEndian) {
        //Console.WriteLine("This machine uses Little Endian processor!");
        SwapEndianism(bin, offset, 4);
        float result = BitConverter.ToSingle(bin, offset);
        //Swap it back:
        SwapEndianism(bin, offset, 4);
        return result;
      }
      else
        return BitConverter.ToSingle(bin, offset);
    }

    public static bool ReadFlag(byte[] bin, int offset)
    {
      byte var = (byte) (0x80 & bin[offset]);
      if (var == 0x80)
        return true;
      else
        return false;
    }

    public static void WriteInt(int value, byte[] target, int offset)
    {
      int net_value = IPAddress.HostToNetworkOrder(value);
      byte[] arr = BitConverter.GetBytes(net_value);
      Array.Copy(arr, 0, target, offset, arr.Length);
    }
    public static void WriteInt(int val, Stream s)
    {
      byte[] data = new byte[4];
      WriteInt(val, data, 0);
      s.Write(data, 0, 4);
    }
    public static void WriteUInt(uint val, byte[] target, int offset)
    {
      for(int i = 0; i < 4; i++) {
        target[offset + i] = (byte)(0xFF & (val >> 8*(3-i)));
      }
    }
    public static void WriteUInt(uint val, Stream s)
    {
      for(int i = 0; i < 4; i++) {
        byte tmp = (byte)(0xFF & (val >> 8*(3-i)));
	s.WriteByte(tmp);
      }
    }

    public static void WriteShort(short value, byte[] target,
                                  int offset)
    {
      short net_value = IPAddress.HostToNetworkOrder(value);
      byte[] arr = BitConverter.GetBytes(net_value);
      Array.Copy(arr, 0, target, offset, arr.Length);
    }
    public static void WriteShort(short val, Stream s)
    {
      byte[] data = new byte[2];
      WriteShort(val, data, 0);
      s.Write(data, 0, 2);
    }
    public static void WriteUShort(ushort val, byte[] target, int offset)
    {
      target[offset] = (byte)( 0xFF & (val >> 8) );
      target[offset + 1] = (byte)( 0xFF & (val) );
    }
    public static void WriteUShort(ushort val, Stream s)
    {
      byte one = (byte)( 0xFF & (val >> 8) );
      byte two = (byte)( 0xFF & (val) );
      s.WriteByte(one);
      s.WriteByte(two);
    }
    /**
     * Write a UTF8 encoding of the string into the byte array
     * and terminate it with a "0x00" byte.
     * @param svalue the string to write into the byte array
     * @param target the byte array to write into
     * @param offset the number of bytes into the target to start
     * @return the number of bytes written
     */
    public static int WriteString(string svalue, byte[] target, int offset)
    {
      Encoding e = Encoding.UTF8;
      int bcount = e.GetBytes(svalue, 0, svalue.Length, target, offset);
      //Write the null:
      target[offset + bcount] = 0;
      return bcount + 1;
    }
    /**
     * Write a UTF8 encoding of the string into the byte array
     * and terminate it with a "0x00" byte.
     * @param svalue the string to write into the byte array
     * @param the Stream to write it into
     * @return the number of bytes written
     */
    public static int WriteString(string svalue, Stream s)
    {
      Encoding e = Encoding.UTF8;
      byte[] data = e.GetBytes(svalue);
      //Write the data:
      s.Write(data, 0, data.Length);
      //Write the null:
      s.WriteByte(0);
      return data.Length + 1;
    }

    public static void WriteLong(int lval, byte[] target, int offset)
    {
      long nval = IPAddress.HostToNetworkOrder(lval);
      byte[] arr = BitConverter.GetBytes(nval);
      Array.Copy(arr, 0, target, offset, arr.Length);
    }
    public static void WriteLong(long val, Stream s)
    {
      for(int i = 0; i < 8; i++) {
        byte tmp = (byte)(0xFF & (val >> 8*(7-i)));
	s.WriteByte(tmp);
      }
    }
    public static void WriteULong(ulong val, Stream s)
    {
      for(int i = 0; i < 8; i++) {
        byte tmp = (byte)(0xFF & (val >> 8*(7-i)));
	s.WriteByte(tmp);
      }
    }

    public static void WriteFloat(float value, byte[] target,
                                  int offset)
    {
      byte[] arr = BitConverter.GetBytes(value);
      if (BitConverter.IsLittleEndian) {
        //Make sure we are Network Endianism
	SwapEndianism(arr, 0, 4);
      }
      Array.Copy(arr, 0, target, offset, 4);
    }

    public static void WriteFlag(bool flag, byte[] target, int offset)
    {
      byte var = target[offset];
      if (flag)
        var |= 0x80;    //Make the first bit 1
      else
        var &= 0x7F;    //Make the first bit 0

      target[offset] = var;
    }

    //Swap the bytes at offset
    protected static void SwapEndianism(byte[] data, int offset, int length)
    {
      int steps = length / 2;
      for(int i = 0; i < steps; i++) {
        byte tmp = data[offset + i];
	data[offset + i] = data[offset + length - i - 1];
	data[offset + length - i - 1] = tmp;
      }
    }

  }

}
