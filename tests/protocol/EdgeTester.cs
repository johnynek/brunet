/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2008 P. Oscar Boykin <boykin@pobox.com>  University of Florida

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


  public class ETServer : IDataHandler
  {
    protected Edge _e;
    public ETServer(Edge e) {
      e.Subscribe(this, null);
      _e = e;
    }
    
    public void HandleData(MemBlock p, ISender edge, object state)
    {
      try {
        int num = NumberSerializer.ReadInt(p, 0);
        Console.WriteLine("Got packet number: {0}", num);
        edge.Send( p );
      }
      catch(Exception x) {
        Console.WriteLine("Server got exception on send: {0}", x);
      }
    }
  }
 
  public class ETClient : IDataHandler
  {
   
    protected Hashtable _sent_blocks; 
    protected object _sync;
    protected Edge _e;
    protected const int count = 32000;

    public ETClient(Edge e)
    {
      _sync = new object();
      _sent_blocks = new Hashtable();
      _e = e;
      _e.Subscribe(this, null );
    }
    
    public  void HandleData(MemBlock p, ISender edge, object state)
    {
      //object count_o = null;
      try {
        lock(_sync) {
          //clear this item out
          //count_o = _sent_blocks[p];
          _sent_blocks.Remove(p);
        }
      }
      catch(Exception x) {
        Console.WriteLine("Error on handling response from {0}: {1}", edge, x);
      }
    }

    /*
     * Start sending packets
     */
    public void Run() {
      byte[] buf = new byte[Packet.MaxLength];
      Random ran_obj = new Random();
      for(int counter = 0; counter < count; counter++) {
        try {
          int size = ran_obj.Next(4, 2048);
          ran_obj.NextBytes(buf);
          NumberSerializer.WriteInt(counter, buf, 0);
          MemBlock cp = MemBlock.Copy(buf, 0, Math.Max(4, counter));
          lock(_sync) { _sent_blocks[cp] = counter; }
          _e.Send( cp );
          Thread.Sleep(10);
          Console.WriteLine("Sending Packet #: " + counter);
        }
        catch(Exception x) {
          Console.WriteLine("send: {0} caused exception: {1}", counter, x);
          break;
        }
      }
      //Let all the responses get back
      Thread.Sleep(5000);
      Check();
      _e.Close();
    }

    public void Check() {
      int missed = 0;
      lock( _sync ) {
        foreach(DictionaryEntry d in _sent_blocks) {
          Console.WriteLine("Packet: {0} not echoed", d.Value);
          missed++;
        }
      }
      Console.WriteLine("Missed: {0}/{1}", missed, count);
    }

  }
    
  public class EdgeTester 
  {
    public static int delay = 20;
   
    public static EdgeListener _el;
    public static bool keep_running;
    public static ArrayList _threads;
    
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

      _threads = ArrayList.Synchronized(new ArrayList());
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
          ClientLoop);
      }
      else if (args[0] == "server") {
        if (args[1] == "tcp") {
          el = new TcpEdgeListener(port);
        }
        else if (args[1] == "udp") {
          el = new UdpEdgeListener(port);
        }
        else {
          el = null;
        }
        el.EdgeEvent += new EventHandler(HandleEdge);
//Start listening:
        el.Start();
        _el = el;
        Console.WriteLine("Press Q to quit");
        Console.ReadLine();
        el.Stop();
      }
      else if (args[0] == "client") {
        TransportAddress ta = null;
        if (args[1] == "tcp") {
          el = new TcpEdgeListener(port + 1);
        }
        else if (args[1] == "udp") {
          el = new UdpEdgeListener(port + 1);
        }
        else {
          el = null;
        }
        ef.AddListener(el);
        _el = el;
        string uri = "brunet." + args[1] + "://" + NameToIP(args[3]) + ":" + port;
        ta = TransportAddressFactory.CreateInstance(uri);
        System.Console.WriteLine("Making edge to {0}\n", ta.ToString());
        el.Start();
        ef.CreateEdgeTo(ta, ClientLoop);
      }
    }

    public static void ClientLoop(bool success, Edge e, Exception ex)
    {
      e.CloseEvent += HandleClose;
      ETClient printer = new ETClient(e);
      Thread t = new Thread(printer.Run);
      _threads.Add(t);
      t.Start();
    }
   
    public static string NameToIP(string name)
    {
      IPAddress[] ips = Dns.GetHostByName(name).AddressList;
      return ips[0].ToString();
    }
    

                /**
         * Handles new edges
         * @param edge the new edge
         * @param args none, we just need it for EventHandlers
         */
    public static void HandleEdge(object edge, EventArgs args)
    {
      Edge e = (Edge) edge;
      e.CloseEvent += HandleClose;
      System.Console.WriteLine("Got an Edge: {0}", edge);
      //The Edge will keep a reference to this ETServer, so it will keep it in
      //scope as long as the EdgeListener does.
      IDataHandler printer = new ETServer(e);
    }
    public static void HandleClose(object edge, EventArgs args)
    {
      Console.WriteLine("Closing edge: {0}", edge );
      _el.Stop();
    }
  }

}
