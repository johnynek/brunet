namespace Brunet {
using System;
using System.Collections;
using GtkSharp;
using Gtk;
using Glade;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
public class ChatConfigSerialization
{

  public ChatConfigSerialization(string user_fn)
  {
    userlistFilename = user_fn;
  }
  
  private string buddylistFilename;
  private BuddyList buddyList; 
  
  public string BuddyListFilename{
    get{
        if (null != buddylistFilename)
          return buddylistFilename;
        else
          throw new Exception();
    }
    set{
        if (null != value)
          buddylistFilename = value;
        else
          throw new Exception();
    }
  }
  public BuddyList BuddyList{
    get{
        if (null != buddyList)
          return buddyList;
        else
          throw new Exception();
    }
    set{
        if (null != value)
          buddyList = value;
        else
          throw new Exception();
    }
  }

  
  private string remoteTasFilename;
  private RemoteTAs remoteTas;
  
  public string RemoteTAsFilename{
    get{
        if (null != remoteTasFilename)
          return remoteTasFilename;
        else
          throw new Exception();
    }
    set{
        if (null != value)
          remoteTasFilename = value;
        else
          throw new Exception();
    }
  }
  public RemoteTAs RemoteTAs{
    get{
        if (null != remoteTas)
          return remoteTas;
        else
          throw new Exception();
    }
    set{
        if (null != value)
          remoteTas = value;
        else
          throw new Exception();
    }
  }

  private string localTcpPortFilename;
  private LocalTcpPort localTcpPort;
  
  public string LocalTcpPortFilename{
    get{
        if (null != localTcpPortFilename)
          return localTcpPortFilename;
        else
          throw new Exception();
    }
    set{
        if (null != value)
          localTcpPortFilename = value;
        else
          throw new Exception();
    }
  }
  public LocalTcpPort LocalTcpPort{
    get{
        if (null != localTcpPort)
          return localTcpPort;
        else
          throw new Exception();
    }
    set{
        if (null != value)
          localTcpPort = value;
        else
          throw new Exception();
    }
  }

  private string userlistFilename;
  private UserList userList; 
  
  public string UserListFilename{
    get{
        if (null != userlistFilename)
          return userlistFilename;
        else
          throw new Exception();
    }
    set{
        if (null != value)
          userlistFilename = value;
        else
          throw new Exception();
    }
  }
  public UserList UserList{
    get{
        if (null != userList)
          return userList;
        else
          throw new Exception();
    }
    set{
        if (null != value)
          userList = value;
        else
          throw new Exception();
    }
  }

  
  public void SerializeBuddyList()
  {
    XmlSerializer buddySerializer = new XmlSerializer(typeof(BuddyList));
    StreamWriter buddyWriter = new StreamWriter(buddylistFilename);
    buddySerializer.Serialize(buddyWriter,buddyList);
    buddyWriter.Close();
  }
  public void DeserializeBuddyList()
  {
    XmlSerializer buddySerializer = new XmlSerializer(typeof(BuddyList));
    StreamReader buddyReader = new StreamReader(buddylistFilename);
    buddyList = (BuddyList)buddySerializer.Deserialize(buddyReader);
    buddyReader.Close();
  }
  
  public void SerializeRemoteTAs()
  {
    XmlSerializer remoteTasSerialer = new XmlSerializer(typeof(RemoteTAs));
    StreamWriter remoteTasWriter = new StreamWriter(remoteTasFilename);
    remoteTasSerialer.Serialize(remoteTasWriter,remoteTas);
    remoteTasWriter.Close();
  }
  public void DeserializeRemoteTAs()
  {
    XmlSerializer remoteTasSerialer = new XmlSerializer(typeof(RemoteTAs));
    StreamReader remoteTasReader = new StreamReader(remoteTasFilename);
    remoteTas = (RemoteTAs)remoteTasSerialer.Deserialize(remoteTasReader);
    remoteTasReader.Close();
  }
  
  public void SerializeLocalTcpPort()
  {
    XmlSerializer localTcpPortSerializer = new XmlSerializer(typeof(LocalTcpPort));
    StreamWriter localTcpPortWriter = new StreamWriter(localTcpPortFilename);
    localTcpPortSerializer.Serialize(localTcpPortWriter,localTcpPort);
    localTcpPortWriter.Close();
  }
  public void DeserializeLocalTcpPort()
  {
    XmlSerializer localTcpPortSerializer = new XmlSerializer(typeof(LocalTcpPort));
    StreamReader localTcpPortReader = new StreamReader(localTcpPortFilename);
    localTcpPort = (LocalTcpPort)localTcpPortSerializer.Deserialize(localTcpPortReader);
    localTcpPortReader.Close();
  }
  
  public void SerializeUserList()
  {
    XmlSerializer userSerializer = new XmlSerializer(typeof(UserList));
    StreamWriter userWriter = new StreamWriter(userlistFilename);
    userSerializer.Serialize(userWriter,userList);
    userWriter.Close();
  }
  public void DeserializeUserList()
  {
    XmlSerializer userSerializer = new XmlSerializer(typeof(UserList));
    StreamReader userReader = new StreamReader(userlistFilename);
    userList = (UserList)userSerializer.Deserialize(userReader);
    userReader.Close();
  }
}
}
