/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

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


namespace Brunet.Tftp {

/**
 * This interface represents the permission system.  When
 * the Agent gets a request, it asks its Acceptor to accept it
 */
public interface IAcceptor {
  /**
   * @param req the Request to allow or deny
   * @param tm the TransferManager to give the decision to (Allow or Deny)
   */
  void Accept(Request req, ITransferManager tm);
}
	
}
