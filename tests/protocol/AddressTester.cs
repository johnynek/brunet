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
 * Brunet.Base32;
 * Brunet.Address;
 * Brunet.AddressParser;
 * Brunet.AHAddress;
 * Brunet.BigInteger;
 * Brunet.DirectionalAddress;
 * Brunet.RwpAddress;
 * Brunet.RwtaAddress;
 * Brunet.NumberSerializer;
 * Brunet.TransportAddress;
 * Brunet.FakeEdge;
 * Brunet.AHPacket;
 * Brunet.Packet;
 */

using Brunet;
using System;

namespace Brunet
{

  public class AddressTester
  {

    public static void Main()
    {
//This part is for testing the ClassOf function of the Address class
      Console.WriteLine("Welcome to the Address Tester program!");

      Console.WriteLine("First we will test Directional Address");
      Console.WriteLine("Left:{0}", new DirectionalAddress(DirectionalAddress.Direction.Left) );
      Console.WriteLine("Right:{0}", new DirectionalAddress(DirectionalAddress.Direction.Right) );

      Console.
        WriteLine
        ("Now, we will test the ClassOf function of the Address class : ");
      byte[]  buf = new byte[20];
      for (int i = 0; i <= 17; i++)
      {
        buf[i] = 0x00;
      }
      buf[18] = 0x03;
        buf[19] = 0x06;
      Address test_address = AddressParser.Parse(buf);

        Console.
        WriteLine
        ("Testing with this address (in integer representation) :  {0}",
         test_address.ToBigInteger().ToString());
        Console.WriteLine("The class of this address is :  {0}",
                          test_address.Class);

//The following is for testing the AHAddress functions
        Console.
        WriteLine
        ("Second, we will test the functions of AHAddress class :  ");
        byte[]  buf1 = new byte[20];
      for (int i = 0; i <= 17; i++)
      {
        buf1[i] = 0x00;
      }
      buf1[18] = 0x00;
      buf1[19] = 0x0A;
      AHAddress test_address_1 = new AHAddress(buf1);

      byte[] buf2 = new byte[20];
      for (int i = 0; i <= 17; i++) {
        buf2[i] = 0xFF;
      }
      buf2[18] = 0xFF;
      buf2[19] = 0xFE;
      AHAddress test_address_2 = new AHAddress(buf2);

      Console.WriteLine("Address 1 :  {0}",
                        test_address_1.ToString());
      Console.WriteLine("Address 2 :  {0}",
                        test_address_2.ToString());
      Console.Write("Class of Address 2 :  {0}",
                    test_address_2.Class);
      Console.Write('\n');

      BigInteger temp = test_address_1.DistanceTo(test_address_2);
      Console.Write("Distance from Address 1 to Address 2 :  {0}",
                    temp.ToString());
      Console.Write('\n');

      Console.Write("Is 1 right of 2 :  {0}",
                    test_address_1.IsRightOf(test_address_2));
      Console.Write('\n');

      Console.Write("Is 1 left of 2 :  {0}",
                    test_address_1.IsLeftOf(test_address_2));
      Console.Write('\n');

//Testing RwpAddress
      Console.
        WriteLine
        ("Third, we will test the functions of the RwpAddress class :  ");
      byte[] buf3 = new byte[20];
      for (int i = 4; i <= 19; i++) {
        buf3[i] = 0xFF;
      }
      Console.WriteLine("Please enter a probability :  ");
      string number = Console.ReadLine();
      NumberSerializer.WriteFloat(float.Parse(number), buf3, 0);
      NumberSerializer.WriteFlag(true, buf3, 4);
      buf3[4] &= 0xBF;  //Make the 34th bit 0
      RwpAddress test_address_3 = new RwpAddress(buf3);

      Console.WriteLine("RwpAddress :  {0}",
                        test_address_3.ToString());
      Console.WriteLine("The class of this address is :  {0}",
                        test_address_3.Class);
      Console.WriteLine("We have flag = {0} and probability = {1} ",
                        test_address_3.Flag, test_address_3.Prob);

//The following are some of the phony TransportAddresses
      TransportAddress home_ta =
        new TransportAddress("brunet.tcp://127.0.27.1:5000");
      TransportAddress ta1 =
        new TransportAddress("brunet.tcp://158.7.0.1:5000");
      TransportAddress ta2 =
        new TransportAddress("brunet.tcp://169.0.5.1:5000");
      TransportAddress ta3 =
        new TransportAddress("brunet.tcp://128.97.89.15:5000");
      TransportAddress ta4 =
        new TransportAddress("brunet.tcp://189.0.47.23:5000");
      TransportAddress ta5 =
        new TransportAddress("brunet.tcp://118.25.87.63:5000");

//Initializing three fake edges
      FakeEdge e1 = new FakeEdge(home_ta, ta1);
      FakeEdge e2 = new FakeEdge(home_ta, ta2);
      FakeEdge e3 = new FakeEdge(home_ta, ta3);
      FakeEdge e4 = new FakeEdge(home_ta, ta4);
      FakeEdge e5 = new FakeEdge(home_ta, ta5);

      System.Collections.ArrayList mylist =
        new System.Collections.ArrayList();
      mylist.Add(e1);
      mylist.Add(e2);
      mylist.Add(e3);
      mylist.Add(e4);
      mylist.Add(e5);


      AHPacket.Protocol prot = AHPacket.Protocol.Connection;
      AHPacket mypacket = new AHPacket(2, 8, test_address_1,
                                       test_address_3, prot, buf1);     //the destination is a RWP address

      bool deliverlocally;

//Testing RwtaAddress
      Console.
        WriteLine
        ("Fourth, we will test the functions of the RwtaAddress class :  ");
      byte[] buf4 = new byte[20];
      for (int i = 0; i <= 19; i++) {
        buf4[i] = 0xFF;
      }
      buf4[0] &= 0x7F;
      RwtaAddress test_address_4 = new RwtaAddress(buf4);
      RwtaAddress test_address_5 = new RwtaAddress();
      Console.WriteLine("The class of this address is :  {0}",
                        test_address_4.Class);
      Console.WriteLine("The class of this address is :  {0}",
                        test_address_5.Class);
      AHPacket mypacket2 = new AHPacket(2, 8, test_address_1,
                                        test_address_4, prot, buf1);    //the destination is a RWTA address


    }

  }

}
