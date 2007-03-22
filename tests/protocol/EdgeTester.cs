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
 * Brunet.Packet;
 * Brunet.ConnectionPacket
 * Brunet.PacketParser;
 * Brunet.Edge;
 * Brunet.EdgeException;
 * Brunet.EdgeListener;
 * Brunet.NumberSerializer
 * Brunet.TcpEdge;
 * Brunet.TcpEdgeListener
 * Brunet.TransportAddress;
 * Brunet.TransportAddress;
 * Brunet.UdpEdgeListener
 * Brunet.EdgeFactory;
 * Brunet.FunctionEdgeListener;
 * Brunet.FunctionEdge;
 * Brunet.BigInteger;
 */

//using log4net;
using Brunet;
using System.Net.Sockets;
using System.Net;
using System;
using System.IO;
using System.Threading;
using System.Collections;

//[assembly: log4net.Config.DOMConfigurator(Watch=true)]
namespace Brunet
{
        /**
	 * This is a testing class that was made to test
	 * out some things, including the TcpEdge and UdpEdge
	 */


  public class ETServer : IPacketHandler
  {
    protected Queue _response_queue;
    
    public ETServer(Queue rq) {
      _response_queue = rq;
    }
    
    public void HandlePacket(Packet p, Edge edge)
    {
      byte[] int_buffer = new byte[4];
      Stream s = p.PayloadStream;
      s.Read(int_buffer, 0, 4);
      int num = NumberSerializer.ReadInt(int_buffer, 0);
      Console.WriteLine("Got packet number: {0}", num);
      lock( _response_queue ) {
        _response_queue.Enqueue(p);
      }
    }

  }
 
  public class ETClient : IPacketHandler
  {
    public int in_counter = 0;
    public Random ran_obj2;
    protected Queue _response_queue;
    public byte[] buf2 = new byte[Packet.MaxLength];
   
    public ETClient(Queue q, int seed)
    {
      _response_queue = q;
      ran_obj2 = new Random(seed);
    }
    
    public  void HandlePacket(Packet p, Edge edge)
    {
     try {
      in_counter++;
      System.Console.WriteLine("Printing packet " + in_counter + ": ");
      System.Console.WriteLine("Length: " + p.Length);
//System.Console.WriteLine( p.ToString() );
      lock( buf2 ) {
      int size2 = ran_obj2.Next(1, Packet.MaxLength);
      ran_obj2.NextBytes(buf2);
      byte[] payload = p.PayloadStream.ToArray();
      Console.WriteLine("Payload length: {0}", payload.Length);
      int sent_count = NumberSerializer.ReadInt(payload, 0);
      System.Console.WriteLine("Sent packet: {0}",sent_count);
      bool cont = (size2 == p.Length) && (sent_count == in_counter);
      int i = 4;
      //Create local with some default values
      byte local = 0, remote = 1;
      while (i < (size2 - 1)
	     && cont)
      {
	//The payload has one less byte than the whole packet
	remote = payload[i];
	i++;
	local = buf2[i];
	if( local != remote )
          cont = false;
      }
      if (!cont) {
        Console.WriteLine("Wrong here!! ");
	Console.WriteLine("my size: {0}",size2);
	Console.WriteLine("local {0}, remote {1}",local, remote);
//	\nmy buffer: {1}", size2,
//			  Convert.ToBase64String(buf2) );
	Console.WriteLine("packet size: {0}",p.Length);
	//\npacket: {1}", p.Length,
	//		  Convert.ToBase64String(p.Buffer, p.Offset, p.Length) );
        //Console.ReadLine();
      }
      else
        Console.WriteLine("Right so far!!");
      }
      //Send an echo back:
      lock( _response_queue ) {
        _response_queue.Enqueue(p);
      }
     }
     catch(Exception x) {
       Console.Error.WriteLine(x.ToString());
     }
    }

  }
	
  public class EdgeTester 
  {
    //private static readonly ILog log = LogManager.GetLogger(typeof(EdgeTester));


    public static readonly int seed = 3;
    //Used by the client:
    public static Random ran_obj = new Random(seed);
    //Used by the server:
    //What packet number is this:
    public static int delay = 20;
    //The buffer used for the server
    public static byte[] int_buffer = new byte[4];
   
    public static Edge in_edge;
    public static EdgeListener _el;
    public static bool keep_running;
    public static Queue response_queue;
    
    public static void Main(string[] args)
    {
      if (args.Length < 3) {
        Console.WriteLine("Usage: edgetester.exe " +
                          "[client|server] [tcp|udp|function] port " +
                          "localhost|qubit|cantor|starsky|behnam|kupka)");
        return;
      }

      if( args.Length >= 5) {
        delay = Int32.Parse(args[4]);
      }

      EdgeFactory ef = new EdgeFactory();
      int port = System.Int16.Parse(args[2]);

      response_queue = new Queue();

      EdgeListener el = null;
      if( args[1] == "function" ) {
        //This is a special case, it only works in one thread
        el = new FunctionEdgeListener(port);
        el.EdgeEvent += new EventHandler(HandleEdge);
        //Start listening:
        el.Start();
	ef.AddListener(el);
	el.CreateEdgeTo(
	     TransportAddressFactory.CreateInstance("brunet.function://localhost:" + port),
	     new EdgeListener.EdgeCreationCallback(ClientLoop));
      }
      else if (args[0] == "server") {
        if (args[1] == "tcp") {
          el = new TcpEdgeListener(port);
        }
        else if (args[1] == "udp") {
          el = new UdpEdgeListener(port);
        }
	else if (args[1] == "function" ) {
	}
        else {
          el = null;
        }
        el.EdgeEvent += new EventHandler(HandleEdge);
//Start listening:
        el.Start();
	_el = el;
      }
      else if (args[0] == "client") {
        TransportAddress ta = null;
        if (args[1] == "tcp") {
          el = new TcpEdgeListener(port + 1);
        }
        else if (args[1] == "udp") {
          el = new UdpEdgeListener(port + 1);
        }
	else if (args[1] == "function" ) {
          el = new FunctionEdgeListener(port + 1);
	}
        else {
          el = null;
        }
	ef.AddListener(el);
	_el = el;
	ta = TransportAddressFactory.CreateInstance( "brunet." + args[1] + "://"
			           + NameToIP(args[3]) + ":" + port );
	System.Console.WriteLine("Making edge to {0}\n", ta.ToString());
	el.Start();
        ef.CreateEdgeTo(ta, 
	     new EdgeListener.EdgeCreationCallback(ClientLoop));
      }
      if( args[0] == "server" ) {
      keep_running = true;
      while(keep_running) {
	lock( response_queue) {
	 while( response_queue.Count > 0 ) {
          try {
            in_edge.Send( (Packet) response_queue.Dequeue() ); 
	  }
	  catch(EdgeException) {
            //The edge is closed
	    keep_running = false;
	    _el.Stop();
	    break;
	  }
	 }
	}
        System.Threading.Thread.Sleep(10);
      }
      }
    }

    public static void ClientLoop(bool success, Edge e, Exception ex)
    {
        if (e == null) {
          System.Console.WriteLine("edge is null");
        }
	IPacketHandler printer = new ETClient(response_queue, seed);
        e.SetCallback(Packet.ProtType.Connection, printer );

        e.CloseEvent += new EventHandler(HandleClose);
	int counter = 0;
	byte[] buf = new byte[Packet.MaxLength];
	int tries = 100;
	try {
         if( ex != null ) throw ex;
         while (tries-- > 0) {
	  lock( response_queue ) {
	   if( counter == 0 || response_queue.Count > 0 )
	   {
	    counter++;
            int size = ran_obj.Next(1, Packet.MaxLength);
            ran_obj.NextBytes(buf);
	    buf[0] = (byte)Packet.ProtType.Connection;
            NumberSerializer.WriteInt(counter, buf, 1);
            ConnectionPacket cp = new ConnectionPacket(buf, 0, size);
            e.Send( cp );
            Console.WriteLine("Sending Packet #: " + counter);
	   }
	  }
          Thread.Sleep(delay);
         }
	}
	catch(EdgeException x) {
          Console.WriteLine("Edge closed: {0}", x);
	}
	//Stop, we should exit here
	_el.Stop();
    }
   
    public static string NameToIP(string name)
    {
      IPAddress[] ips = Dns.GetHostByName(name).AddressList;
      return ips[0].ToString();
#if false 
      string ip;
      switch(name)
      {
        case "qubit":
		ip = "128.97.89.157";
	        break;
        case "cantor":
		ip = "128.97.88.154";
	        break;
	case "starsky":
		ip = "128.97.89.79";
	        break;
	case "behnam":
		ip = "128.97.90.21";
	        break;
        case "kupka":
                ip = "164.67.194.189";
                break;
        case "localhost":
		ip = "127.0.0.1";
	        break;
	default:
		ip = "0.0.0.0";
	        break;
      }
      return ip;
#endif
    }
    

                /**
		 * Handles new edges
		 * @param edge the new edge
		 * @param args none, we just need it for EventHandlers
		 */
    public static void HandleEdge(object edge, EventArgs args)
    {
      Edge e = (Edge) edge;
      e.CloseEvent += new EventHandler(HandleClose);
      System.Console.WriteLine("Got an Edge");
      Console.WriteLine(e.ToString());
      IPacketHandler printer = new ETServer(response_queue);
      e.SetCallback(Packet.ProtType.Connection, printer);
      in_edge = e;
    }
    public static void HandleClose(object edge, EventArgs args)
    {
      ran_obj = new Random(seed);
      Console.WriteLine("Closing edge: " + edge.ToString() );
      _el.Stop();
      keep_running = false;
      return;
    }
  }

}
