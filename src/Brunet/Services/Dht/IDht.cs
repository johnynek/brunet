/*
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet;
using Brunet.Concurrent;
using Brunet.Util;
using System.Collections;

namespace Brunet.Services.Dht {
  public interface IDht {
    /// <summary>Asynchronous create storing the results in the Channel returns.
    /// Creates return true if successful or exception if another value already
    /// exists or there are network errors in adding the entry.</summary>
    /// <param name="key">The index to store the value at.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="ttl">The dht lease time for the key:value pair.</param>
    /// <param name="returns">The Channel where the result will be placed.</param>
    void AsyncCreate(MemBlock key, MemBlock value, int ttl, Channel returns);


    /// <summary>Synchronous create.</summary>
    /// <param name="key">The index to store the value at.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="ttl">The dht lease time for the key:value pair.</param>
    /// <returns>Creates return true if successful or exception if another value
    /// already exists or there are network errors in adding the entry.</returns>
    bool Create(MemBlock key, MemBlock value, int ttl);


    /// <summary>Asynchronous put storing the results in the Channel returns.
    /// Puts return true if successful or exception if there are network errors
    /// in adding the entry.</summary>
    /// <param name="key">The index to store the value at.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="ttl">The dht lease time for the key:value pair.</param>
    /// <param name="returns">The Channel where the result will be placed.</param>
    void AsyncPut(MemBlock key, MemBlock value, int ttl, Channel returns);

    /// <summary>Synchronous put.</summary>
    /// <param name="key">The index to store the value at.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="ttl">The dht lease time for the key:value pair.</param>
    /// <returns>Puts return true if successful or exception if there are
    /// network errors in adding the entry.</returns>
    bool Put(MemBlock key, MemBlock value, int ttl);

    /// <summary>Asynchronous get.  Results are stored in the Channel returns.
    /// </summary>
    /// <param name="key">The index to look up.</param>
    /// <param name="returns">The channel for where the results will be stored
    /// as they come in.</param>
    void AsyncGet(MemBlock key, Channel returns);

    /// <summary>Synchronous get.</summary>
    /// <param name="key">The index to look up.</param>
    /// <returns>An array of Hashtables containing the returnedresults.</returns>
    Hashtable[] Get(MemBlock key);

    string Name { get; }
    bool Online { get; }
  }
}
