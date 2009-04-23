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
using System.Collections.Generic;
using System.Text;

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

    public static string BrunetConfig = "brunet.config";

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
                                                string country) {
      NodeConfig config = Utils.ReadConfig<NodeConfig>(BrunetConfig);
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
      Utils.WriteConfig(BrunetConfig, config);
      WriteToFile(rsa.ExportCspBlob(true), config.Security.KeyPath);
      WriteToFile(cert.X509.RawData, lc_path);
      WriteToFile(cert.X509.RawData, ca_path);

      return cert;
    }

    /**
     * Saves an X509 certificate to the file system.
     * @param cert the X509 certificate
     */
    public static void SaveCertificate(Certificate cert) {
      SocialUser friend = new SocialUser(cert);
      string address = friend.Address.Substring(12);
      NodeConfig config = Utils.ReadConfig<NodeConfig>(BrunetConfig);

      string lc_path = Path.Combine(config.Security.CertificatePath,
                                      "lc" + address + ".cert");
      string ca_path = Path.Combine(config.Security.CertificatePath,
                                      "ca" + address + ".cert");

      if(!Directory.Exists(config.Security.CertificatePath)) {
        Directory.CreateDirectory(config.Security.CertificatePath);
      }
      WriteToFile(cert.X509.RawData, lc_path);
      WriteToFile(cert.X509.RawData, ca_path);
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
     * Create a unique alias for a user resource.
     * @param friends a list of existing alias (users to avoid collisions).
     * @param uid the user unique identifier.
     * @param pcid the pc identifier.
     * @return a unique user alias used for DNS naming.
     */
    public static string CreateAlias(Dictionary<string, SocialUser> friends,
                                     string uid, string pcid) {
      uid = uid.Replace('@', '.');
      string alias = (pcid + "." + uid + ".ipop").ToLower();
      int counter = 1;
      while(friends.ContainsKey(alias)) {
        alias = (pcid + counter + "." + uid + ".ipop").ToLower();
        counter++;
      }
      return alias;
    }

    /**
     * Creates an MD5 string from a byte array.
     * @param data the byte array to be hashed.
     */
    public static string GetMD5(byte[] data) {
      MD5 md5 = new MD5CryptoServiceProvider();
      byte[] result = md5.ComputeHash(data);

      StringBuilder sb = new StringBuilder();
      for (int i=0;i<result.Length;i++) {
        sb.Append(result[i].ToString("X2"));
      }
      return sb.ToString();
    }

  }

#if SVPN_NUNIT
  [TestFixture]
  public class SocialUtilsTester {
    [Test]
    public void SocialUtilsTest() {
      string uid = "ptony82@ufl.edu";
      string pcid = "pdesktop";
      string name = "Pierre St Juste";
      string version = "SVPN_0.3.0";
      string country = "US";

      Certificate cert = SocialUtils.CreateCertificate(uid, name, pcid, 
                                                       version, country);
      SocialUser user = new SocialUser(cert.X509.RawData);

      Assert.AreEqual(uid, user.Uid);
      Assert.AreEqual(name, user.Name);
      Assert.AreEqual(pcid, user.PCID);
      Assert.AreEqual(version, user.Version);
      Assert.AreEqual(country, user.Country);

      SocialUtils.SaveCertificate(cert);
      NodeConfig config = 
        Utils.ReadConfig<NodeConfig>(SocialUtils.BrunetConfig);
      string certPath = System.IO.Path.Combine(config.Security.CertificatePath,
        "lc" + user.Address.Substring(12) + ".cert");
      byte[] certData = SocialUtils.ReadFileBytes(certPath);
      SocialUser tmp = new SocialUser(certData);

      Assert.AreEqual(tmp.Uid, user.Uid);
      Assert.AreEqual(tmp.Name, user.Name);
      Assert.AreEqual(tmp.PCID, user.PCID);
      Assert.AreEqual(tmp.Version, user.Version);
      Assert.AreEqual(tmp.Country, user.Country);
      Assert.AreEqual(tmp.Address, user.Address);
      Assert.AreEqual(tmp.Fingerprint, user.Fingerprint);

    }
  } 
#endif
}
