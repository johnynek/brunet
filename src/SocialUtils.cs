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

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  /**
   * SocialUtils Class. A group of helper functions.
   */
  public class SocialUtils {

    /**
    * The suffix for certificate files.
    */
    public const string CERTSUFFIX = ".cert";

    /**
     * SHA256 hash object.
     */
    protected static readonly SHA256 Sha256;

    /**
     * Constructor.
     */
    static SocialUtils() {
      Sha256 = new SHA256Managed();  
    }

    /**
     * Creates a self-sign X509 certificate based on user parameters.
     * @param uid unique user identifier.
     * @param name user name.
     * @param pcid PC identifier.
     * @param version SocialVPN version.
     * @param country user country.
     * @param certDir the path for the certificate directory
     * @param keyPath the path to the private RSA key
     * @return X509 Certificate.
     */
    public static Certificate CreateCertificate(string uid, string name,
                                                string pcid, string version,
                                                string country, 
                                                string address,
                                                string certDir,
                                                string keyPath) {
      RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();  
      CertificateMaker cm = new CertificateMaker(country, version, pcid,
                                                 name, uid, rsa, address);
      Certificate cert = cm.Sign(cm, rsa);
      
      string lc_path = Path.Combine(certDir, SocialNode.CERTFILENAME);

      if(!Directory.Exists(certDir)) {
        Directory.CreateDirectory(certDir);
      }
      WriteToFile(rsa.ExportCspBlob(true), keyPath);
      WriteToFile(cert.X509.RawData, lc_path);

      return cert;
    }

    /**
     * Saves an X509 certificate to the file system.
     * @param cert the X509 certificate
     */
    public static void SaveCertificate(Certificate cert, string certDir) {
      SocialUser friend = new SocialUser(cert);
      string address = friend.Address.Substring(12);
      string cert_path = Path.Combine(certDir, address + CERTSUFFIX);

      if(!Directory.Exists(certDir)) {
        Directory.CreateDirectory(certDir);
      }
      if(!File.Exists(cert_path)) {
        WriteToFile(cert.X509.RawData, cert_path);
      }
    }

    public static void DeleteCertificate(string address, string certDir) {
      address = address.Substring(12);
      string cert_path = Path.Combine(certDir, address + CERTSUFFIX);
      if(!File.Exists(cert_path)) {
        File.Delete(cert_path);
      }
    }

    /**
     * Reads bytes from a file.
     * @param path file path.
     * @return byte array.
     */
    public static byte[] ReadFileBytes(string path) {
      FileStream file = File.Open(path, FileMode.Open);
      byte[] blob = new byte[file.Length];
      file.Read(blob, 0, (int)file.Length);
      file.Close();
      return blob;
    }

    /**
     * Writes bytes to file.
     * @param data byte array containing data.
     * @param path file path.
     */
    public static void WriteToFile(byte[] data, string path) {
      FileStream file = File.Open(path, FileMode.Create);
      file.Write(data, 0, data.Length);
      file.Close();
    }

    /**
     * Creates object from an Xml string.
     * @param val Xml string representation.
     * @return Object of type T.
     */
    public static T XmlToObject<T>(string val) {
      XmlSerializer serializer = new XmlSerializer(typeof(T));
      T res = default(T);
      using (StringReader sr = new StringReader(val)) {
        res = (T)serializer.Deserialize(sr);
      }
      return res;
    }

    /**
     * Returns an Xml string representation of an object.
     * @param val object to be Xml serialized.
     * @return Xml string representation.
     */
    public static string ObjectToXml<T>(T val) {
      using (StringWriter sw = new StringWriter()) {
        XmlSerializer serializer = new XmlSerializer(typeof(T));
        serializer.Serialize(sw, val);
        return sw.ToString();
      }
    }

    /**
     * Creates a SHA256 hash string from a byte array.
     * @param data the byte array to be hashed.
     * @return Base64 encoded string representing hash.
     */
    public static string GetSHA256(byte[] data) {
      return Convert.ToBase64String(Sha256.ComputeHash(data));
    }

    /**
     * Turns dictionary in www-form-urlencoded.
     * @param parameters the parameters to be encoded.
     * @return urlencoded string.
     */
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

    /**
     * Turn urlencoded string into dictionary.
     * @param request the urlencoded string.
     * @return the dictionary containing parameters.
     */
    public static Dictionary<string, string> DecodeUrl(string request) {
      Dictionary<string, string> result = new Dictionary<string, string>();
      string[] pairs = request.Split('&');

      if (pairs.Length < 2) return result;
      
      for (int x = 0; x < pairs.Length; x++) {
        string[] item = pairs[x].Split('=');
        result.Add(HttpUtility.UrlDecode(item[0]), 
                   HttpUtility.UrlDecode(item[1]));
      }
      return result;
    }

    /**
     * Makes an http request.
     * @param url the url string.
     * @return the http response string.
     */
    public static string Request(string url) {
      return Request(url, (byte[])null);
    }

    /**
     * Makes an http request.
     * @param url the url string.
     * @param parameters the parameters.
     * @return the http response string.
     */
    public static string Request(string url, Dictionary<string, string> 
                                 parameters) {
      ProtocolLog.WriteIf(SocialLog.SVPNLog,
                          String.Format("HTTP REQUEST: {0} {1} {2}",
                          DateTime.Now.TimeOfDay, url, parameters["m"]));
      return Request(url, Encoding.ASCII.GetBytes(UrlEncode(parameters)));
    }

    /**
     * Makes an http request.
     * @param url the url string.
     * @param parameters the byte representation of parameters.
     * @return the http response string.
     */
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

    public static bool ValidateServerCertificate(object sender, 
                                                 X509Certificate certificate,
                                                 X509Chain chain, 
                                                 SslPolicyErrors
                                                 sslPolicyErrors) {
      return true;
    }

    public static void SetSecurityPolicy() {
      ServicePointManager.ServerCertificateValidationCallback = 
        new RemoteCertificateValidationCallback(ValidateServerCertificate);
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
