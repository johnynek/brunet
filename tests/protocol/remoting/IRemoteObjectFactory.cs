using System;

namespace Brunet
{

  public interface IRemoteObjectFactory {
    IRemoteObject Create(int num_nodes);
  }
}
