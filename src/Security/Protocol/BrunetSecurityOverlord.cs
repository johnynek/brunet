/*
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet.Security;
using Mono.Security.X509;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Brunet.Security.Protocol {
  /// <summary>Very thin wrapper used to support AHSenders. This class is
  /// thread-safe.</summary>
  public class ProtocolSecurityOverlord : SecurityOverlord, IRpcHandler {
    protected Node _node;
    protected Dictionary<Address, SecurityAssociation> _address_to_sa;
    protected Dictionary<SecurityAssociation, Address> _sa_to_address;

    public ProtocolSecurityOverlord(Node node,
        RSACryptoServiceProvider rsa,
        ReqrepManager rrman,
        CertificateHandler ch) :
        base(rsa, rrman, ch)
    {
      _node = node;
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
      SecurityAssociation sa = null;
      bool new_sa = false;
      lock(_sync) {
        if(_address_to_sa.ContainsKey(target)) {
          sa = _address_to_sa[target];
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
    protected override SecurityAssociation CreateSecurityAssociation(ISender sender, int spi)
    {
      SecurityAssociation sa = null;
      lock(_sync) {
        sa = base.CreateSecurityAssociation(sender, spi);
        AHSender ahsender = sender as AHSender;
        if(ahsender != null) {
          Address target = ahsender.Destination;
          if(_address_to_sa.ContainsKey(target)) {
            sa = _address_to_sa[target];
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
    protected override void RemoveSA(SecurityAssociation sa)
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

    /// <summary>Overridden because we know the Brunet address.</summary>
    protected override bool Verify(SecurityAssociation sa)
    {
      lock(_sync) {
        if(_sa_to_address.ContainsKey(sa)) {
          return  sa.VerifyCertificateBySubjectAltName(_sa_to_address[sa].ToString());
        }
      }
      return base.Verify(sa);
    }
  }
}
