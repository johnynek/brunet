/*
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet;
using Brunet.Security;
using Brunet.Util;
using CookComputing.XmlRpc;
using System;
using System.Collections;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Ipop {
  /// <summary>Provides the ability to do https (ssl) transactions via web
  /// services and to trust the remote party.</summary>
  public class CertificatePolicy : ICertificatePolicy {
    protected static Hashtable _certs;
    protected static object _sync;
    /// <summary>If we can't return a true for a security exchange, let's pass
    /// the request to the default (original) ICertificatePolicy</summary>
    protected static ICertificatePolicy _old_policy;

    static CertificatePolicy()
    {
      _certs = new Hashtable();
      _sync = new object();
      _old_policy = ServicePointManager.CertificatePolicy;
      ServicePointManager.CertificatePolicy = new CertificatePolicy();
    }

    /// <summary>Register an x509 certificate.</summary>
    public static void Register(X509Certificate cert)
    {
      MemBlock blob = MemBlock.Reference(cert.GetRawCertData());
      lock(_sync) {
        _certs[blob] = true;
      }
    }

    /// <summary>Unregister an x509 certificate.</summary>
    public static void UnRegister(X509Certificate cert)
    {
      MemBlock blob = MemBlock.Reference(cert.GetRawCertData());
      lock(_sync) {
        _certs.Remove(blob);
      }
    }

    /// <summary>Returns true if we have registered the certificate.  Calls the
    /// next ICertificatePolicy if we fail.</summary>
    public bool CheckValidationResult(ServicePoint service_point,
        X509Certificate cert, WebRequest request, int cert_problem)
    {
      MemBlock blob = MemBlock.Reference(cert.GetRawCertData());
      if(_certs.Contains(blob)) {
        return true;
      }
      if(_old_policy != null) {
        return _old_policy.CheckValidationResult(service_point, cert,
           request, cert_problem);
      }
      return false;
    }
  }

  /// <summary>State machine used in obtaining a GroupVPN signed certificate.</summary>
  public class GroupVPNClient {
    protected readonly string _username;
    protected readonly string _group;
    protected readonly string _secret;
    protected string _request_id;
    protected readonly IGroupVPNServer _group_vpn;
    protected readonly byte[] _unsigned_cert;
    protected States _state;
    /// <summary>State of the GroupVPN state machine.</summary>
    public States State { get { return _state; } }
    protected Certificate _cert;
    /// <summary>If State == Finished, we have a valid certificate.</summary>
    public Certificate Certificate { get { return _cert; } }

    /// <summary></summary>
    public enum States {
      /// <summary>Haven't submitted a request for a certificate.</summary>
      Waiting,
      /// <summary>Submitted a request for a certificate.</summary>
      Requested,
      /// <summary>Succeeded in getting the certificate.</summary>
      Finished,
      /// <summary>Failed in getting the certificate.</summary>
      Failed
    };

    public GroupVPNClient(string username, string group, string secret,
        string server_uri, string node_address, RSACryptoServiceProvider public_key)
    {
      _username = username;
      _group = group;
      _secret = secret;
      _state = States.Waiting;

      _group_vpn = (IGroupVPNServer) XmlRpcProxyGen.Create(typeof(IGroupVPNServer));
      _group_vpn.Url = server_uri;

      CertificateMaker cm = new CertificateMaker(string.Empty, string.Empty,
          string.Empty, string.Empty, string.Empty, public_key, node_address);
      _unsigned_cert = cm.UnsignedData;
    }

    /// <summary>Keeps the thread until either we are successful in getting a
    /// certificate or we fail.</summary>
    public void Start()
    {
      while(true) {
        if(_state == States.Waiting) {
          if(SubmitRequest()) {
            _state = States.Requested;
          }
        }

        if(_state == States.Requested) {
          if(CheckRequest()) {
            _state = States.Finished;
          }
        }

        if(_state == States.Finished || _state == States.Failed) {
          break;
        }

        Thread.Sleep(60 * 1000);
      }
    }

    /// <summary>Submits a request to be signed!</summary>
    protected bool SubmitRequest()
    {
      string request_id = string.Empty;

      try {
        request_id = _group_vpn.SubmitRequest(_username, _group, _secret, _unsigned_cert);
      } catch(Exception e) {
        Console.WriteLine(e);
      }

      if(request_id == null || request_id == string.Empty) {
        return false;
      }

      _request_id = request_id;
      return true;
    }

    /// <summary>Checks the request to see if it has been signed!</summary>
    public bool CheckRequest()
    {
      byte[] cert = new byte[0];
      try {
        cert = _group_vpn.CheckRequest(_group, _request_id);
      } catch(Exception e) {
        Console.WriteLine(e);
      }

      if(cert == null || cert.Length == 0) {
        return false;
      }

      _cert = new Certificate(cert);
      return true;
    }
  }

  /// <summary>Provides an XmlRpcClient interface to the GroupVPNServer.</summary>
  public interface IGroupVPNServer : IXmlRpcProxy {
    /// <summary>Submit a request for a certificate.</summary>
    /// <param name="username">Username to assign to the certificate.</param>
    /// <param name="group">Group for certificate.</param>
    /// <param name="secret">A secret used to securely communicate with the proxy.</summary>
    /// <param name="certificate">Base64 encoded certificate.</summary>
    /// <returns>A request id that can be used the check the request.</returns>
    [XmlRpcMethod]
    string SubmitRequest(string username, string group, string secret, byte[] certificate);

    /// <summary>Check and potentially retrieve a certificate.</summary>
    /// <param name="group">Group for certificate.</param>
    /// <param name="request_id">A request id to retrieve a signed certificate.</param>
    /// <returns>NULL if not ready or a Base64 encoded certificate.<returns>
    [XmlRpcMethod]
    byte[] CheckRequest(string group, string request_id);
  }
}
