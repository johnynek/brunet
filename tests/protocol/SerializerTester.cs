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

/*
 * using Brunet.NumberSerializer;
 */

using Brunet;
using System;

namespace Brunet
{

  public class SerializerTester
  {

    public static void Main()
    {

      byte[] buf = new byte[20];

//Testing the WriteFloat and ReadFloat functions
      float num1 = 54.2388F;
        Console.
        WriteLine
        ("We are testing with this number of type float :  {0}",
         num1);
        NumberSerializer.WriteFloat(num1, buf, 2);
      float val1 = NumberSerializer.ReadFloat(buf, 2);
        Console.
        WriteLine
        ("After writing and reading to a byte array, we have :  {0}",
         val1);

//Testing the WriteShort and ReadShort functions
      short num2 = 288;
        Console.
        WriteLine
        ("We are testing with this number of type short :  {0}",
         num2);
        NumberSerializer.WriteShort(num2, buf, 5);
      short val2 = NumberSerializer.ReadShort(buf, 5);
        Console.
        WriteLine
        ("After writing and reading to a byte array, we have :  {0}",
         val2);

//Testing the WriteInt and ReadInt functions
      int num3 = 62;
        Console.
        WriteLine
        ("We are testing with this number of type int :  {0}", num3);
        NumberSerializer.WriteInt(num3, buf, 2);
      int val3 = NumberSerializer.ReadInt(buf, 2);
        Console.
        WriteLine
        ("After writing and reading to a byte array, we have :  {0}",
         val3);

//Testing the ReadFlag function
        Console.
        WriteLine("Please enter an integer between 0 and 255 (147) :  ");
      //string str = Console.ReadLine();
      string str = "147";
        Console.
        WriteLine
        ("For byte >= 128, result is true; for byte <128, result is false");
        buf[2] = byte.Parse(str);
      bool result = NumberSerializer.ReadFlag(buf, 2);
        Console.
        WriteLine
        ("After writing and reading to a byte array, we have :  {0}",
         result);

	AdrConverter.Test();


    }
  }

}
