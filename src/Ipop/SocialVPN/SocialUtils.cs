/*
Copyright (C) 2009 Pierre St Juste <ptony82@ufl.edu>, University of Florida
                   David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Web;
using System.Net;

using Brunet;
using Brunet.Applications;
using Brunet.Security;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace Ipop.SocialVPN {

  public static class SocialUtils {

    public static Certificate CreateCertificate(string uid, string name,
                                                string pcid, string version,
                                                string country, 
                                                string address,
                                                string keyPath) {
      RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();  
      CertificateMaker cm = new CertificateMaker(country, version, pcid,
                                                 name, uid, rsa, address);
      Certificate cert = cm.Sign(cm, rsa);

      if (keyPath != null) {
        string lc_path = "local.cert";
        string lc_path2 = pcid + ".cert";
        WriteToFile(rsa.ExportCspBlob(true), keyPath);
        WriteToFile(cert.X509.RawData, lc_path);
        WriteToFile(cert.X509.RawData, lc_path2);
      }
      return cert;
    }

    public static string GetSHA1HashString(string input) {
      byte[] data = Encoding.Default.GetBytes(input.ToLower());
      return GetSHA1HashString(data);
    }

    public static string GetSHA1HashString(byte[] data) {
      SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider();
      string hash = BitConverter.ToString(sha1.ComputeHash(data));
      hash = hash.Replace("-", "");
      return hash.ToLower();
    }

    public static string GetMD5HashString(string input) {
      byte[] data = Encoding.Default.GetBytes(input.ToLower());
      return GetMD5HashString(data);
    }

    public static string GetMD5HashString(byte[] data) {
      MD5CryptoServiceProvider sha1 = new MD5CryptoServiceProvider();
      string hash = BitConverter.ToString(sha1.ComputeHash(data));
      hash = hash.Replace("-", "");
      return hash.ToLower();
    }

    public static byte[] ReadFileBytes(string path) {
      FileStream file = File.Open(path, FileMode.Open);
      byte[] blob = new byte[file.Length];
      file.Read(blob, 0, (int)file.Length);
      file.Close();
      return blob;
    }

    public static void WriteToFile(byte[] data, string path) {
      FileStream file = File.Open(path, FileMode.Create);
      file.Write(data, 0, data.Length);
      file.Close();
    }

    public static T XmlToObject1<T>(string val) {
      XmlSerializer serializer = new XmlSerializer(typeof(T));
      T res = default(T);
      using (StringReader sr = new StringReader(val)) {
        res = (T)serializer.Deserialize(sr);
      }
      return res;
    }

    public static string ObjectToXml1<T>(T val) {
      using (StringWriter sw = new StringWriter()) {
        XmlSerializer serializer = new XmlSerializer(typeof(T));
        serializer.Serialize(sw, val);
        return sw.ToString();
      }
    }

    // taken from online http://www.dotnetjohn.com/articles.aspx?articleid=173
    public static string ObjectToXml<T>(T val) {
      try {
        String XmlizedString = null;
        MemoryStream memoryStream = new MemoryStream();
        XmlSerializer xs = new XmlSerializer(typeof(T));
        XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, 
                                                        Encoding.UTF8);
        xs.Serialize(xmlTextWriter, val);
        memoryStream = (MemoryStream) xmlTextWriter.BaseStream;
        XmlizedString = UTF8ByteArrayToString(memoryStream.ToArray());
        return XmlizedString;
      } catch ( Exception e ) {
        System.Console.WriteLine(e);
        return null;
      }
    }

    // taken from online http://www.dotnetjohn.com/articles.aspx?articleid=173
    public static T XmlToObject<T>(string val) {
      XmlSerializer xs = new XmlSerializer(typeof(T));
      MemoryStream memoryStream = new MemoryStream(StringToUTF8ByteArray(val));
      return (T) xs.Deserialize(memoryStream);
    }

    public static string UTF8ByteArrayToString(Byte[] characters) {
      UTF8Encoding encoding = new UTF8Encoding();
      String constructedString = encoding.GetString(characters);
      return constructedString;
    }

    public static Byte[] StringToUTF8ByteArray(String pXmlString) {
      UTF8Encoding encoding = new UTF8Encoding ( );
      Byte[ ] byteArray = encoding.GetBytes ( pXmlString );
      return byteArray;
    }

    public static string UrlEncode(Dictionary<string, string> parameters) {
      StringBuilder sb = new StringBuilder();
      int count = 0;
      foreach (KeyValuePair<string, string> de in parameters) {
        count++;
        sb.Append(HttpUtility.UrlEncode(de.Key));
        sb.Append('=');
        sb.Append(HttpUtility.UrlEncode(de.Value));
        if (count < parameters.Count){
          sb.Append('&');
        }
      }

      return sb.ToString();
    }

    public static Dictionary<string, string> DecodeUrl(string request) {
      Dictionary<string, string> result = new Dictionary<string, string>();
      string[] pairs = request.Split('&');

      for (int x = 0; x < pairs.Length; x++) {
        string[] item = pairs[x].Split('=');
        if(item.Length > 1 ) {
          result.Add(HttpUtility.UrlDecode(item[0]), 
                     HttpUtility.UrlDecode(item[1]));
        }
        
      }
      return result;
    }

    public static string Request(string url) {
      return Request(url, (byte[])null);
    }

    public static string Request(string url, Dictionary<string, string> 
                                 parameters) {
      ProtocolLog.WriteIf(SocialLog.SVPNLog,
                          String.Format("HTTP REQUEST: {0} {1} {2}",
                          DateTime.Now.TimeOfDay, url, parameters["m"]));
      return Request(url, Encoding.ASCII.GetBytes(UrlEncode(parameters)));
    }

    public static string Request(string url, byte[] parameters) {
      HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
      webRequest.ContentType = "application/x-www-form-urlencoded";

      if (parameters != null) {
        webRequest.Method = "POST";
        webRequest.ContentLength = parameters.Length;

        using (Stream buffer = webRequest.GetRequestStream()) {
          buffer.Write(parameters, 0, parameters.Length);
          buffer.Close();
        }
      }
      else {
        webRequest.Method = "GET";
      }

      WebResponse webResponse = webRequest.GetResponse();
      using (StreamReader streamReader = 
        new StreamReader(webResponse.GetResponseStream())) {
        return streamReader.ReadToEnd();
      }
    }
  }

#if SVPN_NUNIT
  [TestFixture]
  public class SocialUtilsTester {
    [Test]
    public void SocialUtilsTest() {
      Assert.AreEqual("test", "test");
    }
  } 
#endif
}
