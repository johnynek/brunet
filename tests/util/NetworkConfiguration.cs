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
              System.Console.WriteLine("HERE: {0}", address.Address);
                if ( address.Address.StartsWith(local_host_name) ) {
              System.Console.WriteLine("THERE: {0}, {1}", address.Address,current_index);
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
