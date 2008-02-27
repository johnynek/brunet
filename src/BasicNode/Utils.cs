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
using System.Xml;
using System.Xml.Serialization;

namespace Brunet.Applications {
  /**
   * Provides features commonly necessary in handling configuration of Brunet
   * and features found in BasicNode.
   */
  public class Utils {
    /**
     * Retrieves the geographical location of the host running the process
     * @return the geo location in the form of "latitude, longitude)
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
     * Converts a string of data represented as decimal bytes separated by a 
     * single character (like ip addresses)
     * @param input the string to separate
     * @param sep the separation character (commonly '.' or ',')
     * @return The resulting byte array
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
     * Converts a string of data represented as hex bytes separated by a 
     * single character (like ethernet addresses)
     * @param input the string to separate
     * @param sep the separation character (commonly '.' or ',')
     * @return The resulting byte array
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
     * Converts a MemBlock to a string using char sep inbetween each byte
     * @param input MemBlock to convert to string
     * @param sep the separation character (commonly '.' or ',')
     * @return returns the converted string
     */
    public static string MemBlockToString(MemBlock input, char sep) {
      string return_msg = "";
      for(int i = 0; i < input.Length - 1; i++)
        return_msg += input[i].ToString() + sep.ToString();
      return_msg += input[input.Length - 1];
      return return_msg;
    }

    /**
     * Converts a set of bytes to a string using char sep inbetween each byte
     * @param input byte array to convert to string
     * @param sep the separation character (commonly '.' or ',')
     * @return returns the converted string
     */
    public static string BytesToString(byte [] input, char sep) {
      return MemBlockToString(MemBlock.Reference(input), sep);
    }

    /**
     * Generates a new AHAddress
     * @return returns as type byte[]
     */
    public static byte [] GenerateAddress() {
      AHAddress temp = GenerateAHAddress();
      byte [] tempb = new byte[20];
      temp.CopyTo(tempb);
      return tempb;
    }

    /**
     * Generates a new AHAddress
     * @return returns as type AHAddress
     */
    public static AHAddress GenerateAHAddress() {
      return new AHAddress(new RNGCryptoServiceProvider());
    }

    /**
    <summary>A XML to a generic object of type T, used for configuration
    objects</summary>
    <param name="path">The location of the xml config file to read</param>
    <returns>An object of type T</returns>
    */
    public static T ReadConfig<T>(String path) {
      XmlSerializer serializer = new XmlSerializer(typeof(T));
      T config = default(T);
      using(FileStream fs = new FileStream(path, FileMode.Open)) {
        config = (T) serializer.Deserialize(fs);
      }
      return config;
    }

    /**
    <summary>A generic object to XML, used for configuration objects</summary>
    <param name="path">The full path where the file will be stored.</param>
    <param name="config">An object to be written to the path as an XML file</param>
    */
    public static void WriteConfig(String path, Object config) {
      using(FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write)) {
        XmlSerializer serializer = new XmlSerializer(config.GetType());
        serializer.Serialize(fs, config);
      }
    }
  }
}
