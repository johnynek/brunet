/*
Copyright (C) 2009 Pierre St Juste <ptony82@ufl.edu>, University of Florida

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
using System.Security.Cryptography;
using System.Xml.Serialization;

using Brunet;
using Brunet.Applications;

namespace SocialVPN {

  /**
   * SocialUtils Class. A group of helper functions.
   */
  public class SocialUtils {

    public SocialUtils() {}

    /**
     * Creates a self-sign X509 certificate based on user parameters.
     * @param uid unique user identifier.
     * @param name user name.
     * @param pcid PC identifier.
     * @param version SocialVPN version.
     * @param country user country.
     * @return X509 Certificate.
     */
    public static Certificate CreateCertificate(string uid, string name,
                                                string pcid, string version,
                                                string country, 
                                                string configPath) {
      NodeConfig config = Utils.ReadConfig<NodeConfig>(configPath);
      config.NodeAddress = (Utils.GenerateAHAddress()).ToString();

      RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();  
      CertificateMaker cm = new CertificateMaker(country, version, pcid,
                                                 name, uid, rsa, 
                                                 config.NodeAddress);
      Certificate cert = cm.Sign(cm, rsa);
      
      string lc_path = Path.Combine(config.Security.CertificatePath,
                                    "lc.cert");
      string ca_path = Path.Combine(config.Security.CertificatePath,
                                    "ca.cert");

      if(!Directory.Exists(config.Security.CertificatePath)) {
        Directory.CreateDirectory(config.Security.CertificatePath);
      }
      Utils.WriteConfig(configPath, config);
      WriteToFile(rsa.ExportCspBlob(true), config.Security.KeyPath);
      WriteToFile(cert.X509.RawData, lc_path);
      WriteToFile(cert.X509.RawData, ca_path);

      return cert;
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
  }
}
