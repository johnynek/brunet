using System;
using System.Collections.Generic;
using System.Text;

namespace Ipop {
  /**
   * Contains all non-static methods in BlockingQueue
   */
  public interface IBlockingQueue {
    object Dequeue();
    object Dequeue(int millisec, out bool timedout);
    object Peek();
    object Peek(int millisec, out bool timedout);
    void Enqueue(object o);
    void Close();
  }
}