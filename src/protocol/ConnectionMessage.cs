/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2006 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

/**
 * dependencies
 * Brunet.Packet
 * Brunet.ConnectionPacket
 */

using System;
using System.IO;
using System.Xml;

namespace Brunet
{

  /**
   * A simple request/response protocol for negotiating
   * connections.
   */

  abstract public class ConnectionMessage : IXmlAble
  {

    /**
     * A constructor which reads the data message from
     * a XmlElement.  This should just be the element
     * for the message type, not the whole <request />
     * or <response />
     */
    public ConnectionMessage(System.Xml.XmlElement r)
    {
      //Parse the direction :
      Dir = (ConnectionMessage.Direction) System.Enum.
        Parse(typeof(ConnectionMessage.Direction), r.Name, true);
      //The outer node should be an XmlElement with an "id" attribute :
      Id = 0;
      foreach(XmlNode attr in((XmlElement) r).Attributes) {
        if (attr.Name == "id") {
          //The child of the attribute is a XmlText :
          Id = System.Int32.Parse(attr.FirstChild.Value);
          break;
        }
      }
    }

    public ConnectionMessage()
    {
    }

    /**
     * @return true if this tag is supported
     */
    abstract public bool CanReadTag(string tag);
 
    /**
     * Returns true if o is a ConnectionMessage and Direction and Id match
     */
    override public bool Equals(object o)
    {
      ConnectionMessage co = o as ConnectionMessage;
      if( co != null ) {
        bool same = true;
	same &= co.Dir == this.Dir;
	same &= co.Id == this.Id;
	return same;
      }
      else {
        return false;
      } 
    }
    override public int GetHashCode() {
      return this.Id.GetHashCode();
    }

    static public string GetTagOf(XmlElement r)
    {
      return r.FirstChild.Name;
    }
    
    abstract public IXmlAble ReadFrom(XmlElement el);

    abstract public IXmlAble ReadFrom(XmlReader xr);

    /**
     * this can be used by subclasses to get the XmlReader to the point
     * where ReadPartial can take over.  The beginning of these
     * messages are all the same
     */
    public static void ReadStart(out Direction dir, out int id, XmlReader xr)
    {
      bool finished = false;
      dir = Direction.Undefined;
      id = -1;
      try {
        while(xr.Read() && !finished) {
          if( xr.NodeType == XmlNodeType.Element ) {
            //Look for the <request /> or <response /> tag:
	    /*
	     * Looking for the strings explicitly is very significantly faster
	     * than using Enum.Parse (at least in mono).
	     */
	    if( xr.Name == "request" ) {
              dir = ConnectionMessage.Direction.Request;
	    }
	    else if( xr.Name == "response" ) {
              dir = ConnectionMessage.Direction.Response;
	    }
	    else {
              dir = ConnectionMessage.Direction.Undefined;
	    }
	    id = Int32.Parse(xr["id"]);
	    finished = true;
	  }
        }
      }
      catch(Exception x) {
        throw new ParseException("Could not ReadStart of this ConnectionMessage", x);
      }
      if( !finished ) {
        throw new ParseException("Could not ReadStart of this ConnectionMessage");
      }
    }
    
    virtual public byte[]  ToByteArray()
    {
      //Here is a buffer to write the connection message into :
      MemoryStream s = new MemoryStream(2048);

      XmlWriter w =
        new XmlTextWriter(s, new System.Text.UTF8Encoding());
      w.WriteStartDocument();
      this.WriteTo(w);
      w.WriteEndDocument();
      w.Flush();
      w.Close();
      return s.ToArray();
    }

    virtual public ConnectionPacket ToPacket()
    {
      //Here is a buffer to write the connection message into :
      MemoryStream s = new MemoryStream(2048);
      //This first byte says it is a ConnectionPacket :
      s.WriteByte((byte) Packet.ProtType.Connection);
      XmlWriter w =
        new XmlTextWriter(s, new System.Text.UTF8Encoding());
      w.WriteStartDocument();
      this.WriteTo(w);
      w.WriteEndDocument();
      w.Flush();
      w.Close();
      return new ConnectionPacket(s.ToArray());
    }

    /**
     * Implement Object.ToString().  Basically, we
     * write the message into a StringWriter
     */
    override public string ToString()
    {
      return System.Text.Encoding.UTF8.GetString( ToByteArray() );
    }

    /**
     * Each message should be able to write themselves out
     */
    virtual public void WriteTo(System.Xml.XmlWriter w)
    {
      string xml_ns = "";
      if( Dir == Direction.Request ) {
        w.WriteStartElement("request", xml_ns);
      }
      else if( Dir == Direction.Response ) {
        w.WriteStartElement("response", xml_ns);
      }
      else {
        w.WriteStartElement("undefined", xml_ns);
      }
      w.WriteStartAttribute("id", xml_ns);
      w.WriteString(Id.ToString());
      w.WriteEndAttribute();
    }

    public enum Direction
    {
      Undefined,
      Request,
      Response
    }

    public Direction Dir;
    public int Id;
  }

}
