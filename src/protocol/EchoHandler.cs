/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

namespace Brunet {

/**
 * This is a simple Echo handler.  Useful for doing "ping" type operations.
 */
public class EchoHandler : IDataHandler {

  /**
   * This is a really trivial handler.  It just sends the data
   * back, with PType.Protocol.Echo
   * 
   * It is intended to be used with ReqrepManager
   */
  protected static readonly ICopyable REPLY_HEADER = MemBlock.Reference( new byte[]{1} );
  public void HandleData(MemBlock b, ISender return_path, object state) {
    byte b0 = b[0];
    if( b0 == 0 ) {
      //This is a request:
      MemBlock data = b.Slice(1);
      //Make sure node to reply with a zero
      return_path.Send( new CopyList( PType.Protocol.Echo, REPLY_HEADER, data) );
    }
  }
  
}

}
