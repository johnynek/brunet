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
        WriteLine("Please enter an integer between 0 and 255 :  ");
      string str = Console.ReadLine();
        Console.
        WriteLine
        ("For byte >= 128, result is true; for byte <128, result is false");
        buf[2] = byte.Parse(str);
      bool result = NumberSerializer.ReadFlag(buf, 2);
        Console.
        WriteLine
        ("After writing and reading to a byte array, we have :  {0}",
         result);



    }
  }

}
