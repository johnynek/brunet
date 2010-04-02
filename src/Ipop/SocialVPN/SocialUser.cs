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

namespace Ipop.SocialVPN {

  public class SocialUser {

    public const string PICPREFIX = "http://www.gravatar.com/avatar/";

    private Certificate _cert = null;

    private string _alias = String.Empty;

    private string _fingerprint = String.Empty;

    private string _access = String.Empty;

    private string _ip = String.Empty;

    private string _time = String.Empty;

    private string _status = String.Empty;

    private string _pic = String.Empty;

    private string _certificate = String.Empty;

    public string Uid {
      get { return _cert.Subject.Email.ToLower(); }
      set {}
    }

    public string Name {
      get { return _cert.Subject.Name; }
      set {}
    }

    public string PCID {
      get { return _cert.Subject.OrganizationalUnit; }
      set {}
    }

    public string Country {
      get { return _cert.Subject.Country; }
      set {}
    }

    public string Version {
      get { return _cert.Subject.Organization; }
      set {}
    }

    public string Address {
      get { return _cert.NodeAddress; }
      set {}
    }

    public string Pic {
      get { return _pic; }
      set {}
    }

    public string Fingerprint {
      get { return _fingerprint; }
      set {}
    }

    public string Alias {
      get { return _alias; }
      set {}
    }

    public string IP {
      get { return _ip; }
      set { 
        if(_ip == String.Empty) {
          _ip = value; 
        }
      }
    }

    public string Time {
      get { return _time; }
      set { 
        if(_time == String.Empty) {
          _time = value; 
        }
      }
    }

    public string Access {
      get { return _access; }
      set { 
        if(_access == String.Empty) {
          _access = value; 
        }
      }
    }

    public string Status {
      get { return _status; }
      set { 
        if(_status == String.Empty) {
          _status = value; 
        }
      }
    }

    public string Certificate {
      get { return _certificate; }
      set {
        if(_certificate == String.Empty) {
          _certificate = value;
          byte[] certData = Convert.FromBase64String(value);
          _cert = new Certificate(certData);
          string uid = _cert.Subject.Email.ToLower();
          _fingerprint = SocialUtils.GetSHA1HashString(certData);
          _pic = PICPREFIX + SocialUtils.GetMD5HashString(uid);
          _alias = CreateAlias(PCID, uid);
        }
      }
    }

    public SocialUser() {}

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
      return _cert;
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
