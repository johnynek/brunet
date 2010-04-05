/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007  P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

using Brunet.Util;

namespace Brunet.Messaging {

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
