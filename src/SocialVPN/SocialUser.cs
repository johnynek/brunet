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
using Brunet.Security;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  /**
   * SocialUser Class. Contains information needed to represent a social
   * user.
   */
  public class SocialUser {

    public const string STATUSDEFAULT = "offline";

    public const string IPDEFAULT = "0.0.0.0";

    public const string PICPREFIX = "http://www.gravatar.com/avatar/";

    public string Uid;

    public string Name;

    public string PCID;

    public string Alias;

    public string Address;

    public string Fingerprint;

    public string Country;

    public string Version;

    public string IP;

    public string Status;

    public  string Pic;

    public string Certificate;

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
      Uid = cert.Subject.Email.ToLower();
      Name = cert.Subject.Name;
      PCID = cert.Subject.OrganizationalUnit;
      Address = cert.NodeAddress;
      Version = cert.Subject.Organization;
      Fingerprint = SocialUtils.GetSHA1HashString(cert.X509.RawData);
      Country = cert.Subject.Country;
      Status = STATUSDEFAULT;
      IP = IPDEFAULT;
      Pic = PICPREFIX + SocialUtils.GetMD5HashString(Uid);
      Certificate = Convert.ToBase64String(cert.X509.RawData, 0,
        cert.X509.RawData.Length, Base64FormattingOptions.InsertLineBreaks);
    }
  }

#if SVPN_NUNIT
  [TestFixture]
  public class SocialUserTester {
    [Test]
    public void SocialUserTest() {
      Certificate cert = SocialUtils.CreateCertificate("ptony82@gmail.com",
        "Pierre St Juste", "pcid", "version", "country", "address1234", null);
      SocialUser user = new SocialUser(cert.X509.RawData);
      string xml = SocialUtils.ObjectToXml1<SocialUser>(user);
      Console.WriteLine(xml);
      Assert.AreEqual("test", "test");
    }
  } 
#endif

}
