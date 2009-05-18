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

namespace SocialVPN {

  /**
   * The interface for an identity provider.
   */
  public interface IProvider {

    /**
     * Authenticates a user to a backend.
     * @param id identifier for the identity provider.
     * @param username the username.
     * @param password the password.
     * @return a boolean.
     */
    bool Login(string id, string username, string password);

    /**
     * Retrieves the fingerprints of a particular peer.
     * @param uids the list of user identifiers (i.e. email).
     * @return a list of fingerprints.
     */
    List<string> GetFingerprints(string[] uids);

    /**
     * Retrieves the certificates of a particular peer.
     * @param uids the list of user identifiers (i.e. email).
     * @return a list of certificates.
     */
    List<byte[]> GetCertificates(string[] uids);

    /**
     * Stores the fingerprint of a peer.
     * @return a boolean.
     */
    bool StoreFingerprint();

    /**
     * Validates a certificate
     * @param certData the certificate to be validated
     */
    bool ValidateCertificate(byte[] certData);
  }

  /**
   * The interface for a social network.
   */
  public interface ISocialNetwork {
    /**
     * Authenticates a user to a backend
     * @param id identifier for the identity provider.
     * @param username the username.
     * @param password the password.
     * @return a boolean.
     */
    bool Login(string id, string username, string password);

    /**
     * Get a list of friends from the social network.
     * @return a list of friends.
     */
    List<string> GetFriends();
  }
}
