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
using System.Collections;
using System.IO;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Threading;

namespace Brunet{

  public class RemotingClient{

    public BrunetRemoteObject _ro;
    public TcpChannel _channel;
    //ArrayList _factories;
    Hashtable _host_factory;  
    Hashtable _host_ro;
    Hashtable _hostport_rt;
    Hashtable _hostport_thread;
    Hashtable _hostport_ro;
    ArrayList _hosts;

    public RemotingClient()
    {
      _channel = new TcpChannel();
      _hosts = new ArrayList();
      _host_factory = new Hashtable();  
      _host_ro = new Hashtable();
      _hostport_ro = new Hashtable();
    }

    public void ReadHostNamesFromFile(String host_file_name){
      using( StreamReader ml_sr = new StreamReader(host_file_name) ){
        while (ml_sr.Peek() >= 0){
          _hosts.Add( ml_sr.ReadLine().Trim() );           
        }  
      } 
    }

    public void InitializeRemoteSystem()
    {
      ChannelServices.RegisterChannel(_channel);
      RemoteObjectFactory factory;
      foreach( String host in _hosts){
        factory = (RemoteObjectFactory)Activator.GetObject(typeof(RemoteObjectFactory),
            "tcp://" + host + ":25050/echo");                                  
        _host_factory[host] = factory;       
      }
    }

    public void InitializeRemoteObjects(int num_obj, int num_bn){
      ///read list of machines, for each machine name, lookup the factory
      ///use that factory to create num_obj objects with num_bn of brunet node instances
      RemoteObjectFactory factory;
      BrunetRemoteObject bro;
      foreach( string host in _host_factory.Keys ){
        factory = (RemoteObjectFactory)_host_factory[host];
        int loop = num_obj;
        ArrayList tmp_arr = new ArrayList();
        Console.WriteLine("Host: {0}",host);
        while (loop-- > 0)
        {
          bro = factory.Create(num_bn);

          Console.WriteLine("Factory: {0} SI: {1}",loop,bro._start_index);
          int port = 25000 + bro._start_index;
          int count_down = num_bn;
          while (count_down-- > 0)
          {
            Console.WriteLine("Tester: {0}",port);
            string hostport_str_tmp = host + ":" + Convert.ToString(port);
            string hostport_str = hostport_str_tmp.Trim();
            _hostport_ro[hostport_str] = bro;	 
            Console.WriteLine("hostport_ro count {0} {1}", _hostport_ro.Count ,hostport_str);
            port++;
          }          
          tmp_arr.Add( bro );
          //System.Threading.Thread.Sleep(1000*30);
        }
        _host_ro[host] = tmp_arr;
      }
    }

    public void StartThreads(int interval_sec){
      foreach(string h in _host_ro.Keys)
      {      
        ArrayList bro_arr = (ArrayList)_host_ro[h];
        foreach(BrunetRemoteObject bro in bro_arr )
        {
          bro.StartThreads(interval_sec);
          System.Threading.Thread.Sleep(1000*interval_sec*bro._num_nodes);
        }
      }
    }

    public void KillNode(string hostport)
    {
      BrunetRemoteObject bro = (BrunetRemoteObject)_hostport_ro[hostport];
      string delimStr = ":";
      char [] delimiter = delimStr.ToCharArray();
      string [] str_arr = hostport.Split(delimiter);
      int port = Convert.ToInt32(str_arr[1]);
      
      bro.RemovePort(port);
      _hostport_ro.Remove(hostport);
    }

    public static void Main(string[] args){
      String host_list = args[0];
      int num_obj = Convert.ToInt32( args[1] );
      int num_bn = Convert.ToInt32( args[2] );
      int interval_sec = Convert.ToInt32( args[3] );

      RemotingClient client = new RemotingClient();
      client.ReadHostNamesFromFile( host_list );

      client.InitializeRemoteSystem();    
      
      client.InitializeRemoteObjects(num_obj,num_bn);    

      client.StartThreads(interval_sec);

      Console.WriteLine("Please input the hostname and port to kill in this format:");
      Console.WriteLine("cantor.ee.ucla.edu:8888");

      string line;
      while ((line = Console.ReadLine().Trim()) != null) 
      {
        client.KillNode( line );
        Console.WriteLine("Please input the hostname and port to kill in this format:");
        Console.WriteLine("cantor.ee.ucla.edu:8888");       
      }
    }
  }

}
