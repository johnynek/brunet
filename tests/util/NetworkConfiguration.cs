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
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.Xml.Serialization;

namespace Brunet
{

  public class NetworkConfiguration 
  {
    protected ArrayList _nodes;

    [System.Xml.Serialization.XmlArrayItem("Node",typeof(NodeConfiguration))]
    public ArrayList Nodes
    {
      get
      {
        return _nodes;
      }
      set
      {
        _nodes = value;
      }
    }
    
    public NetworkConfiguration()
    {

    }

    public static NetworkConfiguration Deserialize( String filename )
    {
      XmlSerializer serializer = new XmlSerializer( typeof(NetworkConfiguration) );
      TextReader reader = new StreamReader( filename );
      NetworkConfiguration nc = (NetworkConfiguration)serializer.Deserialize(reader);
      reader.Close();    

      return nc;
    }

    public int GetLocalHostIndex() 
    {
      string local_host_name = Dns.GetHostName();

      if ( _nodes!=null ) {
        int current_index=0;
        foreach(NodeConfiguration nc in _nodes) {
          if ( nc!=null ) {
            ArrayList tas = nc.TransportAddresses;
            if ( tas!=null ) {
              foreach (TransportAddressConfiguration address in tas) {
              //System.Console.WriteLine("HERE: {0}", address.Address);
                if ( address.Address.StartsWith(local_host_name) ) {
              //System.Console.WriteLine("THERE: {0}, {1}", address.Address,current_index);
                  return current_index;
                }
              }
            }
          }
          current_index++;
        }
      }
      return -1;
    }

    public int GetLocalHostIndex(int port_desired) 
    {
      string local_host_name = Dns.GetHostName();

      if ( _nodes!=null ) {
        int current_index=0;
        foreach(NodeConfiguration nc in _nodes) {
          if ( nc!=null ) {
            ArrayList tas = nc.TransportAddresses;
            if ( tas!=null ) {
              foreach (TransportAddressConfiguration address in tas) {
              //System.Console.WriteLine("HERE: {0}", address.Address);
                if ( address.Address.StartsWith(local_host_name) ) {
		  TransportAddressConfiguration local_ta_configuration = (TransportAddressConfiguration)nc.TransportAddresses[0];
		  short port = local_ta_configuration.Port;
	          if(port == port_desired){
              	    //System.Console.WriteLine("THERE: {0}, {1}", address.Address,current_index);
                    return current_index;
                  }
                }
              }
            }
          }
          current_index++;
        }
      }
      return -1;
    }

    public int GetLocalHostIndex(int port_desired, string host_ip) 
    {
      string local_host_name = host_ip;

      if ( _nodes!=null ) {
        int current_index=0;
        foreach(NodeConfiguration nc in _nodes) {
          if ( nc!=null ) {
            ArrayList tas = nc.TransportAddresses;
            if ( tas!=null ) {
              foreach (TransportAddressConfiguration address in tas) {
              //System.Console.WriteLine("HERE: {0}", address.Address);
                if ( address.Address.StartsWith(local_host_name) ) {
		  TransportAddressConfiguration local_ta_configuration = (TransportAddressConfiguration)nc.TransportAddresses[0];
		  short port = local_ta_configuration.Port;
	          if(port == port_desired){
              	    //System.Console.WriteLine("THERE: {0}, {1}", address.Address,current_index);
                    return current_index;
                  }
                }
              }
            }
          }
          current_index++;
        }
      }
      return -1;
    }


  //public static void Main(string[]args)
      //{    

    //NetworkConfiguration nc = NetworkConfiguration.Deserialize( args[0] );
      /*XmlSerializer serializer = new XmlSerializer(typeof(NetworkConfiguration));
      TextWriter writer = new StreamWriter(args[0] + "-d");      
      serializer.Serialize( writer, nc );
      writer.Close();*/

  //  System.Console.WriteLine("{0}", nc.GetLocalHostIndex());

    /*XmlSerializer serializer = new XmlSerializer(typeof(NetworkConfiguration));
      TextWriter writer = new StreamWriter(args[0]);
      ArrayList tas = new ArrayList();
      tas.Add( new TransportAddressConfiguration("tcp","kupka.ee.ucla.edu","5000") );
      NodeConfiguration node_config = new NodeConfiguration();
      node_config.TransportAddresses = tas;

      ArrayList nodes = new ArrayList();
      nodes.Add(node_config);
      
      NetworkConfiguration nc = new NetworkConfiguration();
      nc.Nodes = nodes;

      serializer.Serialize( writer, nc );
      writer.Close();        */

    //}
  }

}
