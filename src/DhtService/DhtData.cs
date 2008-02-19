/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Text;
using System.Collections;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

/* These need to be merged */

namespace Ipop {
  public class DhtData {
    public string key, value, ttl;
  }

  public class DhtDataHandler {
    public static DhtData Read(string configFile) {
      FileStream fs = new FileStream(configFile, FileMode.Open, FileAccess.Read);
      StreamReader sr = new StreamReader(fs);
      DhtData data = new DhtData();
      while(sr.Peek() >= 0) {
        string line = sr.ReadLine();
        int equal_pos = line.IndexOf("=");
        string type = line.Substring(0, equal_pos);
        string value = line.Substring(equal_pos + 1, line.Length - equal_pos - 1);
        switch(type) {
          case "key":
            data.key = value;
            break;
          case "value":
            data.value = value;
            break;
          case "ttl":
            data.ttl = value;
            break;
        }
      }
      sr.Close();
      fs.Close();
      return data;
    }

    public static void Write(string configFile, DhtData data) {
      FileStream fs = new FileStream(configFile, FileMode.Create, 
        FileAccess.Write);
      StreamWriter sw = new StreamWriter(fs);
      sw.WriteLine("key=" + data.key);
      sw.WriteLine("value=" + data.value);
      sw.WriteLine("ttl=" + data.ttl);
      sw.Close();
      fs.Close();
    }
  }
}
