/*
Copyright (C) 2009 Pierre St Juste <ptony82@ufl.edu>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
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
      for(int i = 0; i < parts.Length; i++) {
        alias += parts[i] + ".";
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
