using System;
using System.IO;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.Xml.Serialization;

namespace Brunet
{

  public class TransportAddressConfiguration 
  {
    protected String _protocol;
    protected String _address;
    protected short _port;

    public String Protocol
    {
      get
      {
        return _protocol;
      }
      set
      {
        _protocol = value;
      }
    }

    public String Address
    {
      get
      {
        return _address;
      }
      set
      {
        _address = value;
      }
    }

    public short Port
    {
      get
      {
        return _port;
      }
      set
      {
        _port = value;
      }
    }

    public TransportAddressConfiguration()
    {
    
    }

    public TransportAddressConfiguration(String protocol, String address, short port)
    {
      _protocol = protocol;
      _address = address;
      _port = port;
    }

    public String GetTransportAddressURI()
    {
      String ta = "brunet." + _protocol + "://" + _address + ":" + _port ; 
      return ta;
    }

  /*public static void Main(String[] args)
    {
      XmlSerializer serializer = new XmlSerializer(typeof(TransportAddressConfiguration));
      TextWriter writer = new StreamWriter(args[0]);
      serializer.Serialize(writer,new TransportAddressConfiguration("tcp","kupka.ee.ucla.edu",5000));
      writer.Close();      
      }*/

  }

}
