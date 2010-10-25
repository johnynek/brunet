// Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida
// // For license, see the file LICENSE in the root directory of this software.
//
using Brunet.Util;

namespace Brunet.Messaging {
  public interface IFilter : ISource, IDataHandler
  {
  }

  public class SimpleFilter : SimpleSource, IFilter
  {
    virtual public void HandleData(MemBlock b, ISender return_path, object state)
    {
      Handle(b, return_path);
    }
  }
}
