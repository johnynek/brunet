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
using System.Net;

namespace Brunet
{

  /**
   * Reads numbers in and out byte arrays
   */
  public class NumberSerializer
  {

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

    public static float ReadFloat(byte[] bin, int offset)
    {
      if (BitConverter.IsLittleEndian) {
        //Console.WriteLine("This machine uses Little Endian processor!");
        byte[] arr = new byte[4];
        for (int i = 0; i < arr.Length; i++)
          arr[i] = bin[offset + arr.Length - 1 - i];
        return BitConverter.ToSingle(arr, 0);
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

    public static void WriteShort(short value, byte[] target,
                                  int offset)
    {
      short net_value = IPAddress.HostToNetworkOrder(value);
      byte[] arr = BitConverter.GetBytes(net_value);
      Array.Copy(arr, 0, target, offset, arr.Length);
    }

    public static void WriteLong(int lval, byte[] target, int offset)
    {
      long nval = IPAddress.HostToNetworkOrder(lval);
      byte[] arr = BitConverter.GetBytes(nval);
      Array.Copy(arr, 0, target, offset, arr.Length);
    }

    public static void WriteFloat(float value, byte[] target,
                                  int offset)
    {
      byte[] arr = new byte[4];
      arr = BitConverter.GetBytes(value);
      if (BitConverter.IsLittleEndian) {
        for (int i = 0; i < arr.Length; i++)
          target[i + offset] = arr[arr.Length - 1 - i];
      }
      else {
        for (int i = 0; i < arr.Length; i++)
          target[i + offset] = arr[i];
      }
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


  }

}
