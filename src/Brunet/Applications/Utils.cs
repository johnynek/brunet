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

using Brunet;
using System.Net;
using System.Security.Cryptography;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Sockets;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

using Brunet.Symphony;
namespace Brunet.Applications {
  /**
  Provides features commonly necessary in handling configuration of Brunet and
  features found in BasicNode.
  */
  public class Utils {
    /**
    <summary>Retrieves the geographical location of the host running the
    process by communicating with a service provided by www.grid-appliance.org.
    </summary>
    <returns>The geo location in the form of "latitude, longitude".</returns>
    */
    public static string GetMyGeoLoc() {
      try {
        string server = "www.grid-appliance.org";
        int port = 80;
        Regex lat = new Regex("Latitude.+");
        Regex lon = new Regex("Longitude.+");
        Regex num = new Regex("\\-{0,1}\\d+.\\d+");
        Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        s.Connect(server, port);
        string request = "GET /ip2geo/geo.php HTTP/1.1\r\nHost: www.grid-appliance.org\r\n\r\n";
        byte[] bs = Encoding.ASCII.GetBytes(request);
        s.Send(bs, bs.Length, 0);
        string page = String.Empty;
        byte[] br = new byte[256];
        int bytes = 0;
        do {
          bytes = s.Receive(br, br.Length, 0);
          page += Encoding.ASCII.GetString(br, 0, bytes);
        } while (bytes > 0);

        Match latm = lat.Match(page);
        Match lonm = lon.Match(page);
        if(latm.Success && lonm.Success) {
          latm = num.Match(latm.Value);
          lonm = num.Match(lonm.Value);
          if(latm.Success && lonm.Success) {
            return latm.Value + ", " + lonm.Value;
          }
        }
      }
      catch{}
      return ",";
    }

    /**
    <summary>Converts a string of data represented as decimal bytes separated
    by a single character (like ip addresses)</summary>
    <param name="input">The string to parse.</param>
    <param name="sep">The separation character (commonly '.' or ',').</param>
    <returns>The converted byte array.</returns>
     */
    public static byte [] StringToBytes(string input, char sep) {
      char [] separator = {sep};
      string[] ss = input.Split(separator);
      byte [] ret = new byte[ss.Length];
      for (int i = 0; i < ss.Length; i++) {
        ret[i] = byte.Parse(ss[i].Trim());
      }
      return ret;
    }

    /**
    <summary>Converts a string of data represented as hex bytes separated by a 
    single character (like ethernet addresses: FE:F0:00:00:01:02)</summary>
    <param name="input">The string to parse.</param>
    <param name="sep">The separation character (commonly '.' or ',')</param>
    <returns>The converted byte array</returns>
    */
    public static byte [] HexStringToBytes(string input, char sep) {
      char [] separator = {sep};
      string[] ss = input.Split(separator);
      byte [] ret = new byte[ss.Length];
      for (int i = 0; i < ss.Length; i++) {
        ret[i] = byte.Parse(ss[i].Trim(), System.Globalization.NumberStyles.HexNumber);
      }
      return ret;
    }

    /**
    <summary>Converts a MemBlock to a string using char sep inbetween each
    byte.</summary>
    <param name="input">MemBlock to convert to string</param>
    <param name="sep"> the separation character (commonly '.' or ',')</param>
    <returns>The converted string.</returns>
    */
    public static string MemBlockToString(Brunet.Util.MemBlock input, char sep) {
      string return_msg = "";
      for(int i = 0; i < input.Length - 1; i++)
        return_msg += input[i].ToString() + sep.ToString();
      return_msg += input[input.Length - 1];
      return return_msg;
    }

    /**
    <summary>Converts a set of bytes to a string using char sep inbetween each
    byte.</summary>
    <param name="input">Byte array to convert to string</param>
    <param name="sep"> the separation character (commonly '.' or ',')</param>
    <returns>The converted string</returns>
    */
    public static string BytesToString(byte [] input, char sep) {
      return MemBlockToString(Brunet.Util.MemBlock.Reference(input), sep);
    }

    /**
    <summary>Generates a new AHAddress as a byte array.</summary>
    <returns>An unique AHAddress as type byte[].</returns>
    */
    public static byte [] GenerateAddress() {
      AHAddress temp = GenerateAHAddress();
      byte [] tempb = new byte[20];
      temp.CopyTo(tempb);
      return tempb;
    }

    /**
    <summary>Generates an unique AHAddress.</summary>
    <returns>An unique AHAddress</returns>
    */
    public static AHAddress GenerateAHAddress() {
      return new AHAddress(new RNGCryptoServiceProvider());
    }

    /// <summary>A XML to a generic object of type T, used for configuration
    /// objects</summary>
    /// <param name="path">The location of the xml config file to read</param>
    /// <returns>An object of type T</returns>
    public static T ReadConfig<T>(String path) {
      XmlSerializer serializer = new XmlSerializer(typeof(T));
      T config = default(T);
      using(FileStream fs = new FileStream(path, FileMode.Open)) {
        config = (T) serializer.Deserialize(fs);
      }

      object o = (object) config;
      SetAllObjects(ref o);
      return (T) o;
    }

    ///<summary>Sets all fields to their non-null default value.  This
    ///is unnecessary if a constructor already does this.</summary>
    ///<param name="o_to_set">The object whose fields we want to be default
    ///non-null.</param>
    protected static void SetAllObjects(ref object o_to_set) {
      Type t = o_to_set.GetType();
      foreach(FieldInfo fi in t.GetFields()) {
        if(!fi.IsPublic) {
          continue;
        }
        if(fi.GetValue(o_to_set) == null) {
          ConstructorInfo ci = fi.FieldType.GetConstructor(new Type[0]);
          if(ci == null) {
            continue;
          }
          object r_set = ci.Invoke(null);
          SetAllObjects(ref r_set);
          fi.SetValue(o_to_set, r_set);
        }
      }
    }

    /// <summary>A generic object to XML, used for configuration objects</summary>
    /// <param name="path">The full path where the file will be stored.</param>
    /// <param name="config">An object to be written to the path as an XML file</param>
    public static void WriteConfig(String path, Object config) {
      using(FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write)) {
        XmlSerializer serializer = new XmlSerializer(config.GetType());
        serializer.Serialize(fs, config);
      }
    }

    /// <summary>Makes a deep copy of an object, similar to a C++ copy
    /// constructor.</summary>
    public static T Copy<T>(T tobject) {
      T tcopy = default(T);

      using(Stream ms = new MemoryStream()) {
        XmlSerializer serializer = new XmlSerializer(typeof(T));
        serializer.Serialize(ms, tobject);
        ms.Position = 0;
        tcopy = (T) serializer.Deserialize(ms);
      }
      return tcopy;
    }
  }
}
