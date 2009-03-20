/*
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet;
using System.Collections;

namespace Brunet.DistributedServices {
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
  }
}
