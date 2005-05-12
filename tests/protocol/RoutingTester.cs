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

//Brunet.Address
//Brunet.AHAddress;
//Brunet.ConnectionType;
//Brunet.FakeEdge;
//Brunet.TransportAddress;
//Brunet.Edge;
//Brunet.Node

using Brunet;
using System;

namespace Brunet
{

  public class RoutingTester
  {

/*
 * This is a testing program for the AHRoutingTable
 * It tests all the functions of the AHRoutingTable class
 */

    public static void Main()
    {
    ///@todo Make this class work with AHRouter and ConnectionTable
#if false
//Initializing the AHAddress of the node on which AHRoutingTable is at
      byte[] buf = new byte[20];
      for (int i = 0; i <= 17; i++)
      {
        buf[i] = 0x00;
      }
      buf[18] = 0x00;
        buf[19] = 0x0A;
      AHAddress home_address = new AHAddress(buf);

        Console.Write('\n');
        Console.WriteLine("AHAddress of the Routing Table :  {0}",
                          home_address.ToBigInteger().ToString());


///@todo fix this code with the new additions to AHRoutingTable
      AHRoutingTable test_table = new AHRoutingTable(null, null);

//Initializing three AHAddresses for the three test Edges
        byte[]  buf1 = new byte[20];
      for (int i = 0; i <= 17; i++)
      {
        buf1[i] = 0xFF;
      }
      buf1[18] = 0xFF;
      buf1[19] = 0xFE;
      Console.WriteLine("Class of :  {0} is: {1}", buf1.ToString(),
                        Address.ClassOf(buf1));
      AHAddress address1 = new AHAddress(buf1);

      byte[] buf2 = new byte[20];
      for (int i = 0; i <= 17; i++) {
        buf2[i] = 0x00;
      }
      buf2[18] = 0x00;
      buf2[19] = 0x04;
      Console.WriteLine("Class of :  {0} is: {1}", buf2.ToString(),
                        Address.ClassOf(buf2));
      AHAddress address2 = new AHAddress(buf2);

      byte[] buf3 = new byte[20];
      for (int i = 0; i <= 17; i++) {
        buf3[i] = 0x00;
      }
      buf3[18] = 0x00;
      buf3[19] = 0x0C;
      Console.WriteLine("Class of :  {0} is: {1}", buf3.ToString(),
                        Address.ClassOf(buf3));
      AHAddress address3 = new AHAddress(buf3);

      byte[] buf6 = new byte[20];
      for (int i = 0; i <= 17; i++) {
        buf6[i] = 0x00;
      }
      buf6[18] = 0xF0;
      buf6[19] = 0xAC;
      Console.WriteLine("Class of :  {0} is: {1}", buf6.ToString(),
                        Address.ClassOf(buf6));
      AHAddress address4 = new AHAddress(buf6);

      byte[] buf5 = new byte[20];
      for (int i = 0; i <= 17; i++) {
        buf5[i] = 0x00;
      }
      buf5[18] = 0x00;
      buf5[19] = 0x0E;
      Console.WriteLine("Class of :  {0} is: {1}", buf5.ToString(),
                        Address.ClassOf(buf5));
      AHAddress address5 = new AHAddress(buf5);




      Console.WriteLine("Creating Transport Addresses");
//The following are some of the phony TransportAddresses
      TransportAddress home_ta =
        new TransportAddress("brunet.tcp : //127.0.27.1 : 5000");
      TransportAddress ta1 =
        new TransportAddress("brunet.tcp : //158.7.0.1 : 5000");
      TransportAddress ta2 =
        new TransportAddress("brunet.tcp : //169.0.5.1 : 5000");
      TransportAddress ta3 =
        new TransportAddress("brunet.tcp : //128.97.89.157 : 5000");
      TransportAddress ta4 =
        new TransportAddress("brunet.tcp : //189.0.47.23 : 5000");
      TransportAddress ta5 =
        new TransportAddress("brunet.tcp : //118.25.87.63 : 5000");

      Console.WriteLine("Creating Edges");
//Initializing three fake edges
      FakeEdge e1 = new FakeEdge(home_ta, ta1);
      FakeEdge e2 = new FakeEdge(home_ta, ta2);
      FakeEdge e3 = new FakeEdge(home_ta, ta3);
      FakeEdge e4 = new FakeEdge(home_ta, ta4);
      FakeEdge e5 = new FakeEdge(home_ta, ta5);

      Console.WriteLine("Adding to Table : ");
//Testing the Add function
      test_table.Add(address1, e1);
      test_table.Add(address2, e2);
      test_table.Add(address3, e3);
      test_table.Add(address4, e4);
      test_table.Add(address5, e5);

      Console.WriteLine("AHAddress of Edge 1 is :  {0}",
                        address1.ToBigInteger().ToString());
      Console.WriteLine("AHAddress of Edge 2 is :  {0}",
                        address2.ToBigInteger().ToString());
      Console.WriteLine("AHAddress of Edge 3 is :  {0}",
                        address3.ToBigInteger().ToString());
      Console.WriteLine("AHAddress of Edge 4 is :  {0}",
                        address4.ToBigInteger().ToString());
      Console.WriteLine("AHAddress of Edge 5 is :  {0}",
                        address5.ToBigInteger().ToString());

      Console.
        WriteLine
        ("Please enter destination address (integer < 256 only)");
      string num = Console.ReadLine();
//Testing the FindClosestTo function
      byte[] buf4 = new byte[20];
      for (int i = 0; i <= 17; i++) {
        buf4[i] = 0x00;
      }
      buf4[18] = 0x00;
      buf4[19] = byte.Parse(num);

      AHAddress dest_add = new AHAddress(buf4);

      Console.WriteLine("AHAddress of destination is :  {0}",
                        dest_add.ToBigInteger().ToString());

      Console.
        WriteLine
        ("Now testing the FindClosestTo function with 2 arguments");

      bool del_local1;
      Edge e_temp =
        test_table.FindClosestTo(dest_add, out del_local1);
      FakeEdge e = e_temp as FakeEdge;
      Console.
        WriteLine("Are we delivering locally?  The answer is :  {0}",
                  del_local1);

      if (e.Equals(e1))
        Console.WriteLine("Succeed = Edge 1!!");
      else if (e.Equals(e2))
        Console.WriteLine("Succeed = Edge 2!!");
      else if (e.Equals(e3))
        Console.WriteLine("Succeed = Edge 3!!");
      else if (e.Equals(e4))
        Console.WriteLine("Succeed = Edge 4!!");
      else if (e.Equals(e5))
        Console.WriteLine("Succeed = Edge 5!!");
      else
        Console.WriteLine("What is going on?");

//Testing the second FindClosestTo function
      bool del_local2;
      Edge e_temp2 =
        test_table.FindClosestTo(dest_add, e3, out del_local2);
      FakeEdge e_var = e_temp2 as FakeEdge;

      Console.
        WriteLine
        ("Now testing the FindClosestTo function with 3 arguments");
      Console.
        WriteLine("Are we delivering locally? The answer is :  {0}",
                  del_local2);
      if (e_var == null)
        Console.WriteLine("No closer edge is found!");
      else if (e_var.Equals(e1))
        Console.WriteLine("Succeed = Edge 1!!");
      else if (e_var.Equals(e2))
        Console.WriteLine("Succeed = Edge 2!!");
      else if (e_var.Equals(e3))
        Console.WriteLine("Succeed = Edge 3!!");
      else if (e_var.Equals(e4))
        Console.WriteLine("Succeed = Edge 4!!");
      else if (e_var.Equals(e5))
        Console.WriteLine("Succeed = Edge 5!!");
      else
        Console.WriteLine("What is going on?");

      Console.
        WriteLine
        ("Now testing the remove function, we will remove Edge 1");
      test_table.Remove(e1);

      Console.WriteLine("We will now remove Edge 2");
      test_table.Remove(e2);

      Console.WriteLine("We will now remove Edge 4");
      test_table.Remove(e4);
#endif
    }

  }

}
