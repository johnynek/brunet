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

    public readonly string Uid;

    public readonly string Name;

    public readonly string PCID;

    public readonly string Address;

    public readonly string Fingerprint;

    public readonly string DhtKey;

    public readonly string Country;

    public readonly string Version;

    public string Alias;

    public string IP;

    public string Time;

    public string Access;

    public  string Pic;

    public enum AccessTypes {
      Approved = 1,
      Unapproved = 2,
      Denied = 3,
      Pending = 4
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
      Fingerprint = SocialUtils.GetSHA1(cert.X509.RawData);
      DhtKey = "svpn:" + Uid + ":" + Fingerprint;
      Country = cert.Subject.Country;
      IP = "0.0.0.0";
      Time = "0";
      Alias = "Unknown";
      Pic = "pic";
    }
  }

#if SVPN_NUNIT
  [TestFixture]
  public class SocialUserTester {
    [Test]
    public void SocialUserTest() {
      string uid = "ptony82@ufl.edu";
      string name = "Pierre St Juste";
      string pcid = "pdesktop";
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

      Console.WriteLine(user.Fingerprint);
    }
  } 
#endif

}
