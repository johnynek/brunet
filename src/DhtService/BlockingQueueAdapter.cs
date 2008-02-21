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

using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.Runtime.Remoting;
using CookComputing.XmlRpc;
using Brunet;

namespace Brunet.Rpc {  
  public class BlockingQueueAdapter : MarshalByRefObject, IBlockingQueue {
    /**
     * Adaptee
     */
    [NonSerialized]
    protected BlockingQueue _bq;

    public BlockingQueueAdapter(BlockingQueue bq) {
      this._bq = bq;
    }

    public object Dequeue() {
      return this._bq.Dequeue();
    }
    public object Dequeue(int millisec, out bool timedout) {
      return this._bq.Dequeue(millisec, out timedout);
    }
    public void Close() {
      this._bq.Close();
    }

    public object Peek() {
      return this._bq.Peek();
    }

    public object Peek(int millisec, out bool timedout) {
      return this._bq.Peek(millisec, out timedout);
    }

    public void Enqueue(object o) {
      this._bq.Enqueue(o);
    }
  }
}