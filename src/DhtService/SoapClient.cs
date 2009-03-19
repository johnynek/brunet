/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;
using Brunet.DistributedServices;
using System;
using System.Collections;

namespace Brunet.DhtService {
  public partial class DhtServiceClient {
    public static ISoapDht GetSoapDhtClient(int port) {
      return (ISoapDht)Activator.GetObject(typeof(ISoapDht),
              "http://127.0.0.1:" + port + "/sd.rem");
    }
  }

  public interface ISoapDht : IRpcDht {
    IBlockingQueue GetAsBlockingQueue(byte[] key);
  }
}
