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

#if BRUNET_NUNIT
using NUnit.Framework;
using System.IO;
#endif

using System.Xml;

namespace Brunet {

/**
 * Objects which implement this interface
 * handle basic XML reading and writing of
 * those objects.  Read: XML-able
 */
  public interface IXmlAble {

    /**
     * @return true if we can read xml objects in the given tag
     */
    bool CanReadTag(string tag);
    /**
     * Read the object stored in this element and return it
     * @return 
     */
    IXmlAble ReadFrom(System.Xml.XmlElement encoded);
	    
    /**
     * Write into w.  Does not close w after the writing.
     */
    void WriteTo(System.Xml.XmlWriter w);
	
  }
#if BRUNET_NUNIT
/**
 * A class to test any XmlAble object
 */
  public class XmlAbleTester {
    public XmlAbleTester()
    {

    }

    /**
     * @param a The object to be serialized then deserialized
     * @return should be an exact copy of a.  So that it a.Equals is true.
     */
    public IXmlAble SerializeDeserialize(IXmlAble a)
    {
      //Write the object out:
      MemoryStream s = new MemoryStream(1024);
      XmlWriter w = new XmlTextWriter(s, new System.Text.UTF8Encoding());
      w.WriteStartDocument();
      a.WriteTo(w);
      w.WriteEndDocument();
      w.Flush();
      //w.Close();

      //Seek to the beginning, so we can read it in:
      s.Seek(0, SeekOrigin.Begin);
      XmlDocument doc = new XmlDocument();
      doc.Load(s);
      XmlNode r = doc.FirstChild;
      //Find the first element :
      foreach(XmlNode n in doc.ChildNodes) {
        if (n is XmlElement) {
          r = n;
          break;
        }
      }
      return a.ReadFrom((XmlElement)r);
    }
  }
#endif


}
