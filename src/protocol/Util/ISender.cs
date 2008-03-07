/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

namespace Brunet {

/**
 * Represents objects which can send ICopyable objects.
 */
public interface ISender {

  /**
   * Send some copyable object somewhere.  This is half of our basic communication
   * primative.  This is may be asychronous, it may throw an exception.
   */
  void Send(ICopyable data);

}

/**
 * When MemBlock objects are received, this is how we handle them.  Generally,
 * communications will happen asynchronously.
 */
public interface IDataHandler {
  /**
   * Generally, you will pass objects of this type to "subscribe" to incoming
   * packets or responses.  When subscribing, the state is passed as well.  When
   * data comes, the state is passed back allowing the handler to know something
   * about where the data came from, or the type of the data
   *
   * @param b the data which needs to be handled.
   * @param return_path a sender which can send data back the source of b
   * @param state some object which is meaningful to the handler
   */
  void HandleData(MemBlock b, ISender return_path, object state);
}
}
