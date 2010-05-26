/*
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System;

namespace Brunet.Messaging {
  /// <summary>Implementing this interface eases deployment of a new channel
  /// that lacks easy ways to identify multiple streams on the same channel.
  /// </summary>
  public interface IIdentifierPair {
    /// <summary>The ID for the local peer, locally set.</summary>
    int LocalID { get; set; }
    /// <summary>The ID for the remote peer, remotely set.</summary>
    int RemoteID { get; set; }
    /// <summary>The IDs prepackaged.</summary>
    MemBlock Header { get; }
  }

  /// <summary>Implements a simple IIdentifierPair, suggestion:  if classes
  /// already implement a class, have them implement IIdentifierPair and forward
  /// the requests to an internal (protected) IdentifierPair.</summary>
  public class IdentifierPair : IIdentifierPair {
    /// <summary>UidGenerator sets that as unset, so this will be the default ID.</summary>
    public const int DEFAULT_ID = 0;
    /// <summary>Offset in the header for the source Identifier.</summary>
    public const int SOURCE_OFFSET = 0;
    /// <summary>Offset in the header for the destination Identifier.</summary>
    public const int DESTINATION_OFFSET = 4;

    protected object _sync;

    public int LocalID {
      get {
        return _local_id;
      }
      set {
        lock(_sync) {
          if(_local_id != DEFAULT_ID) {
            // Idempotency
            if(_local_id == value) {
              return;
            }
            throw new Exception("ID already set");
          }
          _local_id = value;
          UpdateHeader();
        }
      }
    }

    public int RemoteID {
      get {
        return _remote_id;
      }
      set {
        lock(_sync) {
          if(_remote_id != DEFAULT_ID) {
            // Idempotency
            if(_remote_id == value) {
              return;
            }
            throw new Exception("ID already set");
          }
          _remote_id = value;
          UpdateHeader();
        }
      }
    }

    public MemBlock Header { get { return _header; } }

    protected int _local_id;
    protected int _remote_id;
    protected MemBlock _header;

    public IdentifierPair()
    {
      _sync = new object();
      lock(_sync) {
        _local_id = DEFAULT_ID;
        _remote_id = DEFAULT_ID;
        UpdateHeader();
      }
    }

    /// <summary>Change in either local or remote IDs, time to update the
    /// header to match.</summary>
    protected void UpdateHeader()
    {
      byte[] header = new byte[8];
      NumberSerializer.WriteInt(_local_id, header, SOURCE_OFFSET);
      NumberSerializer.WriteInt(_remote_id, header, DESTINATION_OFFSET);
      _header = MemBlock.Reference(header);
    }
  }
}
