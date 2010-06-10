/*
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet.Messaging;
using Brunet.Util;
using Mono.Security.X509;
using System;
using System.Collections;
using System.Security.Cryptography;

namespace Brunet.Security {
  /// <summary> A certificate handler for GroupVPN, provides the ability to revoke
  /// certificates by username alone rather then revoking individual certificates.</summary>
  public class BroadcastRevocationHandler : ICertificateVerification, IDataHandler {
    public static readonly PType PType = new PType("broadcast_revocation");
    protected Hashtable _revoked_users;
    protected readonly Certificate _ca_cert;
    protected readonly CertificateHandler _ch;
    protected readonly SecurityOverlord _so;

    public BroadcastRevocationHandler (Certificate ca_cert, SecurityOverlord so)
    {
      _revoked_users = new Hashtable();
      _ca_cert = ca_cert;
      _so = so;
    }

    /// <summary>Parses an incoming revocation and updates the revoked users
    /// hashtable if successful.</summary>
    public void HandleData(MemBlock data, ISender ret, object state)
    {
      UserRevocationMessage urm = null;
      try {
        urm = new UserRevocationMessage(_ca_cert, data);
      } catch(Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.SecurityExceptions, e.ToString());
        return;
      }


      lock(_revoked_users) {
        if(_revoked_users.Contains(urm.Username)) {
          return;
        }
        _revoked_users[urm.Username] = true;
      }

      _so.VerifySAs();
    }

    /// <summary>True upon a non-revoked certificate, an exception otherwise.</summary>
    public bool Verify(X509Certificate x509, Brunet.Messaging.ISender sender)
    {
      Certificate cert = new Certificate(x509.RawData);
      if(!_revoked_users.Contains(cert.Subject.Name)) {
        return true;
      }
      throw new Exception("User has been revoked!");
    }
  }
}
