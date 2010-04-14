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
using Brunet.Concurrent;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace Ipop.SocialVPN {

  public class SocialUser {

    public const string PICPREFIX = "http://www.gravatar.com/avatar/";

    private WriteOnce<Certificate> _cert;

    private WriteOnce<string> _alias;

    private WriteOnce<string> _fingerprint;

    private WriteOnce<string> _access;

    private WriteOnce<string> _ip;

    private WriteOnce<string> _time;

    private WriteOnce<string> _status;

    private WriteOnce<string> _pic;

    private WriteOnce<string> _certificate;

    public string Uid {
      get { return _cert.Value.Subject.Email.ToLower(); }
      set {}
    }

    public string Name {
      get { return _cert.Value.Subject.Name; }
      set {}
    }

    public string PCID {
      get { return _cert.Value.Subject.OrganizationalUnit; }
      set {}
    }

    public string Country {
      get { return _cert.Value.Subject.Country; }
      set {}
    }

    public string Version {
      get { return _cert.Value.Subject.Organization; }
      set {}
    }

    public string Address {
      get { return _cert.Value.NodeAddress; }
      set {}
    }

    public string Pic {
      get { return _pic.Value; }
      set {}
    }

    public string Fingerprint {
      get { return _fingerprint.Value; }
      set {}
    }

    public string Alias {
      get { return _alias.Value; }
      set {}
    }

    public string IP {
      get { return _ip.Value; }
      set { _ip.Value = value;}
    }

    public string Time {
      get { return _time.Value; }
      set { _time.Value = value; }
    }

    public string Access {
      get { return _access.Value; }
      set { _access.Value = value; }
    }

    public string Status {
      get { return _status.Value; }
      set { _status.Value = value; }
    }

    public string Certificate {
      get { return _certificate.Value; }
      set {
        _certificate.Value = value;
        byte[] certData = Convert.FromBase64String(value);
        _cert.Value = new Certificate(certData);
        string uid = _cert.Value.Subject.Email.ToLower();
        _fingerprint.Value = SocialUtils.GetSHA1HashString(certData);
        _pic.Value = PICPREFIX + SocialUtils.GetMD5HashString(uid);
        _alias.Value = CreateAlias(PCID, uid);
      }
    }

    public SocialUser() {
      _cert = new WriteOnce<Certificate>();
      _alias = new WriteOnce<string>();
      _fingerprint = new WriteOnce<string>();
      _access = new WriteOnce<string>();
      _ip = new WriteOnce<string>();
      _time = new WriteOnce<string>();
      _status = new WriteOnce<string>();
      _pic = new WriteOnce<string>();
      _certificate = new WriteOnce<string>();
    }

    protected static string CreateAlias(string pcid, string uid) {
      char[] delims = new char[] {'@','.'};
      string[] parts = uid.Split(delims);
      string alias = String.Empty;
      for(int i = 0; i < parts.Length-1; i++) {
        alias += parts[i] + ".";
      }
      if(parts[parts.Length-1] == "com") {
        alias += "com.";
      }
      alias = (pcid + "." + alias + SocialNode.DNSSUFFIX).ToLower();
      return alias;
    }

    public Certificate GetCert() {
      return _cert.Value;
    }

    public SocialUser WeakCopy() {
      SocialUser user = new SocialUser();
      user.Certificate = this.Certificate;
      return user;
    }

    public SocialUser ChangedCopy(string ip, string time, string access,
      string status) {
      SocialUser user = WeakCopy();
      user.IP = ip;
      user.Time = time;
      user.Access = access;
      user.Status = status;
      return user;
    }

    public SocialUser ExactCopy() {
      return ChangedCopy(IP, Time, Access, Status);
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
