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

using Brunet;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  /**
   * SocialUser Class. Contains information needed to represent a social
   * user.
   */
  public class SocialUser {

    public const string TIMEDEFAULT = "0";

    public const string IPDEFAULT = "0.0.0.0";

    public const string ALIASDEFAULT = "unknown";

    public const string PICDEFAULT = "nopic";

    public const string DHTPREFIX = "svpn";

    public string Uid;

    public string Name;

    public string PCID;

    public string Address;

    public string Fingerprint;

    public string DhtKey;

    public string Country;

    public string Version;

    public string Alias;

    public string IP;

    public string Time;

    public string Access;

    public  string Pic;

    public enum AccessTypes {
      Allow = 1,
      Block = 2,
      Pending = 3
    }

    public SocialUser() {}

    /**
     * Constructor.
     * @param certData X509 certificate bytes.
     */
    public SocialUser(byte[] certData): this(new Certificate(certData)) {}

    /**
     * Constructor that takes an X509 certificate.
     * @param cert X509 certificate.
     */
    public SocialUser(Certificate cert) {
      Uid = cert.Subject.Email;
      Name = cert.Subject.Name;
      PCID = cert.Subject.OrganizationalUnit;
      Address = cert.NodeAddress;
      Version = cert.Subject.Organization;
      Fingerprint = SocialUtils.GetSHA256(cert.X509.RawData);
      DhtKey = DHTPREFIX + ":" + PCID + ":" + Uid + ":" + Fingerprint;
      Country = cert.Subject.Country;
      Access = AccessTypes.Block.ToString();
      Time = TIMEDEFAULT;
      IP = IPDEFAULT;
      Alias = ALIASDEFAULT;
      Pic = PICDEFAULT;
   }

  /**
   * Override ToString method.
   * @return the string representation of SocialUser.
   */
  public override string ToString() {
    string dl = "\n";
    return Uid + dl + Name + dl + PCID + dl + Address + dl + Version + dl + 
     DhtKey + dl + Country + dl + Access + dl + Time + dl + IP + dl + 
     Alias + dl + Pic;
   }
  }

#if SVPN_NUNIT
  [TestFixture]
  public class SocialUserTester {
    [Test]
    public void SocialUserTest() {
      Assert.AreEqual("test", "test");
    }
  } 
#endif

}
