using System;
using System.Collections.Generic;
using System.Text;

namespace Ipop {
  public interface IBlockingQueue {
    object Dequeue();
    object Dequeue(int millisec, out bool timedout);
    void Close();
  }
}
