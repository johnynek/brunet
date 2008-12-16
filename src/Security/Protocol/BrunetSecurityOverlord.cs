using Mono.Security.X509;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Brunet.Security {
  public class BrunetSecurityOverlord : SecurityOverlord, IRpcHandler {
    protected Node _node;
    protected Dictionary<Address, SecurityAssociation> _address_to_sa;
    protected Dictionary<SecurityAssociation, Address> _sa_to_address;

    public BrunetSecurityOverlord(Node node,
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

    public void HandleRpc(ISender caller, string method, IList args, object rs)
    {
      object result = null;
      try {
        if(method.Equals("AddCertificate")) {
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
