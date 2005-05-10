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

namespace Brunet{
using System.Xml.Serialization;
public class BrunetEventDescriptor
{
  /** Enum of the connection type defined in the Brunet 
   * namespace
   *
   */
  private ConnectionType _connection_type;

  private string _sub_type;

  /** Description of the event e.g. 
   *  connection or disconnection
   */
  private string _event_desc;  

  /** Base-32 encoded binary string of the ahaddress
   * of the local node
   */
  private string _local_ah_address;
  
  /** Base-32 encoded binary string of the ahaddress
   * of the remote node
   */
  private string _remote_ah_address;
  
  private string _local_ta_address;
  private string _local_port;
  private string _remote_ta_address;
  private string _remote_ah_address_base32;


  /** This 64-bit integer is the output of the
   * System.DateTime.Ticks property
   */
  private long _connect_time;
  private int _structure_degree;
  
  [XmlAttribute("EventDescription")] 
  public string EventDescription
  {
    get{
      return _event_desc;
    }
    set
    {
      _event_desc = value;
    }
  }	 

  [XmlAttribute("ConnectionType")] 
  public ConnectionType ConnectionType
  {
    get
    {
      return _connection_type;
    }
    set
    {
      _connection_type = value;
    }
  }	  

  [XmlAttribute("SubType")] 
  public string SubType
  {
    get
    {
      return _sub_type;
    }
    set
    {
      _sub_type = value;
    }
  }	
  
  [XmlAttribute("LocalAHAddress")] 
  public string LocalAHAddress
  {
    get{
      return _local_ah_address;
    }
    set
    {
      _local_ah_address = value;
    }
  }	  
  [XmlAttribute("LocalPort")] 
  public string LocalPort
  {
    get{
      return _local_port;
    }
    set
    {
      _local_port = value;
    }
  }
  [XmlAttribute("RemoteAHAddress")] 
  public string RemoteAHAddress
  {
    get
    {
      return _remote_ah_address;
    }
    set
    {
      _remote_ah_address = value;
    }
  }

  [XmlAttribute("LocalTAddress")] 
  public string LocalTAddress
  {
    get{
      return _local_ta_address;
    }
    set
    {
      _local_ta_address = value;
    }
  }	  
  [XmlAttribute("RemoteTAddress")] 
  public string RemoteTAddress
  {
    get
    {
      return _remote_ta_address;
    }
    set
    {
      _remote_ta_address = value;
    }
  }

  [XmlAttribute("RemoteAHAddressBase32")] 
  public string RemoteAHAddressBase32
  {
    get
    {
      return _remote_ah_address_base32;
    }
    set
    {
      _remote_ah_address_base32 = value;
    }
  }
	  
  [XmlAttribute("ConnectTime")] 
  public long ConnectTime{
    get
    {
      return _connect_time;
    }
    set
    {
      _connect_time = value;
    }
  } 

  [XmlAttribute("StructureDegree")] 
  public int StructureDegree{
    get
    {
      return _structure_degree;
    }
    set
    {
      _structure_degree = value;
    }
  } 
  
  public BrunetEventDescriptor(){
  }
  
  public BrunetEventDescriptor(string desc, ConnectionType ct,string la,string ra, long t)
  {
    _event_desc = desc;
    _connection_type = ct;
    _local_ah_address = la;
    _remote_ah_address = ra;
    _connect_time =t;
  }



}

}
