/*
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using Brunet.Symphony;
using Brunet.Util;
using Mono.Security.X509;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Brunet.Security.PeerSec.Symphony {
  /// <summary>Very thin wrapper used to support AHSenders. This class is
  /// thread-safe.</summary>
  public class SymphonySecurityOverlord : PeerSecOverlord, IRpcHandler {
    protected Node _node;
    protected Dictionary<Address, SecurityAssociation> _address_to_sa;
    protected Dictionary<SecurityAssociation, Address> _sa_to_address;

    public SymphonySecurityOverlord(Node node, RSACryptoServiceProvider rsa,
        CertificateHandler ch, ReqrepManager rrman) :
      base(rsa, ch, rrman)
    {
      _ch.AddCertificateVerification(new SymphonyVerification(ch));
      _node = node;
      node.EdgeVerifyMethod = EdgeVerify.AddressInSubjectAltName;
      _address_to_sa = new Dictionary<Address, SecurityAssociation>();
      _sa_to_address = new Dictionary<SecurityAssociation, Address>();
      lock(_sync) {
        _node.Rpc.AddHandler("Security", this);
      }
    }

    // Provides a method for local apps to add certificates to Brunet without
    // being loaded with Brunet.
    public void HandleRpc(ISender caller, string method, IList args, object rs)
    {
      object result = null;
      try {
        if(method.Equals("AddCertificate")) {
          ReqrepManager.ReplyState rqrs = caller as ReqrepManager.ReplyState;
          if(rqrs == null || !(rqrs.ReturnPath is Node)) {
            throw new Exception("Call must be made locally for security reasons!");
          }
          string path = (string) args[0];
          result = _ch.AddCertificate(path);
        } else {
          throw new Exception("Invalid method");
        }
      } catch (Exception e) {
        result = new AdrException(-32602, e);
      }
      _node.Rpc.SendResult(rs, result);
    }

    // Provides an Exact AH Secure Sender using the default SPI  given an address
    public SecurityAssociation GetSecureSender(Address target)
    {
      PeerSecAssociation sa = null;
      bool new_sa = false;
      lock(_sync) {
        if(_address_to_sa.ContainsKey(target)) {
          sa = _address_to_sa[target] as PeerSecAssociation;
        } else {
          AHSender sender = new AHExactSender(_node, target);
          sa = base.CreateSecurityAssociation(sender, SecurityPolicy.DefaultSPI);
          _address_to_sa[target] = sa;
          _sa_to_address[sa] = target;
          new_sa = true;
        }
      }

      if(new_sa) {
        StartSA(sa);
      }

      return sa;
    }

    // We override the underlying method so that we can properly wrap incoming AHSenders
    override protected PeerSecAssociation CreateSecurityAssociation(ISender sender, int spi)
    {
      PeerSecAssociation sa = null;
      lock(_sync) {
        sa = base.CreateSecurityAssociation(sender, spi);
        AHSender ahsender = sender as AHSender;
        if(ahsender != null) {
          Address target = ahsender.Destination;
          if(_address_to_sa.ContainsKey(target)) {
            sa = _address_to_sa[target] as PeerSecAssociation;
          } else {
            sa = base.CreateSecurityAssociation(sender, spi);
            _address_to_sa[target] = sa;
            _sa_to_address[sa] = target;
          }
        }
      }
      return sa;
    }

    /// <summary>Removes the specified SA from our database.</summary>
    override protected void RemoveSA(SecurityAssociation sa)
    {
      lock(_sync) {
        AHSender sender = sa.Sender as AHSender;
        if(sender != null) {
          _address_to_sa.Remove(sender.Destination);
          _sa_to_address.Remove(sa);
        }
        base.RemoveSA(sa);
      }
    }
  }
}
