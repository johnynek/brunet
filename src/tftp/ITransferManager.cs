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

public interface ITransferManager {

  /**
   * Deny the request
   * @param req the Request we are denying
   * @param reason the Error to send to our peer
   */
  void Deny(Request req, Error reason);

  /**
   * Allow the request and write or read the data from the given
   * stream
   * @param req the request to allow
   * @param data the stream to read the file from
   */
  Status Allow(Request req, System.IO.Stream data);
	
}
	
}
