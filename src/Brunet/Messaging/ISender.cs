/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

using System;
using System.Collections;

using Brunet.Util;
namespace Brunet.Messaging {

/**
 * Represents objects which can send ICopyable objects.
 */
public interface ISender {

  /**
   * Send some copyable object somewhere.  This is half of our basic communication
   * primative.  This is may be asychronous, it may throw an exception.
   * @throws SendException if there is some problem
   */
  void Send(ICopyable data);
 /**
   * Converts the sender into a URI representation.
   */
  string ToUri();
  
}

/** Some senders "wrap" other senders, subclass this
 */
public interface IWrappingSender {
  ISender WrappedSender {
    get;
  } 
}

public class SendException : Exception {

  public readonly bool IsTransient;

  public SendException(bool istransient) : base() {
    IsTransient = istransient;
  }
  
  public SendException(bool istransient, string message) : base(message) {
    IsTransient = istransient;
  }
  public SendException(bool istransient, string message, Exception inner) : base(message, inner) {
    IsTransient = istransient;
  }

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
