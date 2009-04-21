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

    // Protected variables
    protected string _uid;
    protected string _name;
    protected string _pcid;
    protected string _address;
    protected string _version;
    protected string _fingerprint;
    protected string _country;
    protected string _ip;
    protected string _alias;
    protected string _time;
    protected string _access;

    // Accessors
    public string Uid { get { return _uid; } }
    public string Name { get { return _name; } }
    public string PCID { get { return _pcid; } }
    public string Address { get { return _address; } }
    public string Fingerprint { get { return _fingerprint; } }
    public string Country { get { return _country; } }
    public string Version { get { return _version; } }

    public string IP {
      get { return _ip; }
      set { _ip = value; }
    }

    public string Alias {
      get { return _alias; }
      set { _alias = value; }
    }

    public string Time {
      get { return _time; }
      set { _time = value; }
    }

    public string Access {
      get { return _access; }
      set { _access = value; }
    }

    // Constructors
    protected SocialUser() {}

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
      _uid = cert.Subject.Email;
      _name = cert.Subject.Name;
      _pcid = cert.Subject.OrganizationalUnit;
      _address = cert.NodeAddress;
      _version = cert.Subject.Organization;
      _fingerprint = Convert.ToBase64String(cert.Signature);
      _country = cert.Subject.Country;
    }

    /**
     * Constructor based on user input.
     * @param uid unique user identifier.
     * @param name user name.
     * @param pcid PC identifier.
     * @param address user p2p address.
     * @param version SocialVPN version.
     * @param fingerprint X509 Certificate signature
     * @param country user country.
     */
    public SocialUser(string uid, string name, string pcid, string address,
                      string version, string fingerprint, string country) {
      _uid = uid;
      _name = name;
      _pcid = pcid;
      _address = address;
      _version = version;
      _fingerprint = fingerprint;
      _country = country;
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
    }
  } 
#endif

}
