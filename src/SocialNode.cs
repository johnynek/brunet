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
using System.Collections;
using System.Collections.Generic;

using Brunet;
using Brunet.Applications;
using Ipop.RpcNode;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  /**
   * SocialNode Class. Extends the RpcIpopNode to support adding friends based
   * on X509 certificates
   */
  public class SocialNode : RpcIpopNode {

    /**
     * Dictionary of friends indexed by alias
     */
    private Dictionary<string, SocialUser> _friends;

    /**
     * The collection of friends
     */
    public ICollection Friends { get { return _friends.Values; } }
    /**
     * Constructor.
     * @param brunetConfig configuration file for Brunet P2P library.
     * @param ipopConfig configuration file for IP over P2P app.
     */
    public SocialNode(string brunetConfig, string ipopConfig) :
      base(brunetConfig, ipopConfig) {
      _friends = new Dictionary<string, SocialUser>();
      Init();
    }

    /**
     * Add local certificates to security handler.
     */
    public void Init() {
      _bso.CertificateHandler.AddCertificate("lc.cert");  
      _bso.CertificateHandler.AddCertificate("ca.cert");  
    }

    /**
     * Add a friend to socialvpn from an X509 certificate.
     * @param cert the X509 certificate.
     */
    public void AddFriend(Certificate cert) {
      SocialUser friend = new SocialUser(cert);
      Address addr = AddressParser.Parse(friend.Address);
      friend.Alias = SocialUtils.CreateAlias(friend.Uid, friend.PCID);

      // Save certificate to file system
      SocialUtils.SaveCertificate(cert);

      // Pass certificates to security handler through paths
      string lc_path = "lc" + friend.Address.Substring(12) + ".cert";
      string ca_path = "ca" + friend.Address.Substring(12) + ".cert";
      _bso.CertificateHandler.AddCertificate(lc_path);
      _bso.CertificateHandler.AddCertificate(ca_path);

      // Add friend to the network
      friend.IP = _rarad.RegisterMapping(friend.Alias, addr);

      // Add friend to list
      _friends.Add(friend.Alias, friend);
    }
  }
}
