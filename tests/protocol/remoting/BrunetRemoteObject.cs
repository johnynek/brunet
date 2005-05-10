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
using System.Net;
using System.Threading;

namespace Brunet
{

  public class BrunetRemoteObject : MarshalByRefObject {
  
    public int _start_index;
    public int _num_nodes;
    public Hashtable _port_thread = new Hashtable();
    public Hashtable _port_rt = new Hashtable();

    public BrunetRemoteObject(int start_index, int num_nodes)
    {
      _start_index = start_index;
      _num_nodes = num_nodes;
      string xml_file = "TestNetwork.brunet";
      for( int i = 0; i < num_nodes; i++){
        int desired_port = start_index + i + 25000;
        
        String _connection_log_file = "./data/" + "brunetadd" + Convert.ToString(desired_port) + ".log";
        if(File.Exists(_connection_log_file)){
          File.Delete(_connection_log_file);
        }
        StreamWriter fs = new StreamWriter(_connection_log_file, true);
        fs.AutoFlush = true;

        NetworkConfiguration network_configuration = NetworkConfiguration.Deserialize(xml_file);
        RemotingTester rt = new RemotingTester(desired_port, network_configuration, fs); 

        Thread backgroundThread = new Thread(new ThreadStart(rt.StartBrunet));
        _port_thread[desired_port] = backgroundThread;
        _port_rt[desired_port] = rt;
        Console.WriteLine(" Count {0}:",_port_rt.Count);
        //System.Threading.Thread.Sleep(1000*3);
      }   
    }

    public void StartThreads(int seconds)
    {
      foreach(int port in _port_thread.Keys)
      {
        ((Thread)_port_thread[port]).Start();
        System.Threading.Thread.Sleep(1000*seconds);
      }
    }

    public void RemovePort(int port)
    {
      try
      {
        ((Thread)_port_thread[port]).Abort();
      }
      catch(ThreadAbortException e){}
      
      ((RemotingTester)_port_rt[port]).node.Disconnect();
      WriteDeletion( port );
      _port_thread.Remove(port);
      _port_rt.Remove(port);
    }
    
    public void WriteDeletion(int port){
      RemotingTester rt = (RemotingTester)_port_rt[port];
      rt._sw.Write( DateTime.Now.ToUniversalTime().ToString("MM'/'dd'/'yyyy' 'HH':'mm':'ss") + 
          ":" + DateTime.Now.ToUniversalTime().Millisecond +
					    "  deletion  deletion  " + rt.node.Address.ToBigInteger().ToString() + '\n');
	  }

  }

}

