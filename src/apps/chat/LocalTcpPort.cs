using System;	
using System.IO;
using System.Xml.Serialization;

namespace Brunet{

public class LocalTcpPort
{
  private int port;
  
  public LocalTcpPort()
  {
  }
  public LocalTcpPort(int a_port)
  {
    port = a_port;
  }

  [XmlArrayItem(ElementName="LocalTcpPort",Type=typeof(int))]
  public int TcpPort {
    get{
      return port;
    }
    set{
      port = value;
    }
  }
}
}
