using System.Collections.Generic;

namespace Brunet.Transport {
  /// <summary>An interface for a node that has TAs it wants to share and is
  /// actively looking for remote tas.</summary>
  public interface ITAHandler {
    /// <summary>The nodes Local TAs.</summary>
    IList<TransportAddress> LocalTAs { get; }
    /// <summary>TAs for remote peers.</summary>
    IList<TransportAddress> RemoteTAs { get; }
    /// <summary>A method to update the list of remote TAs.</summary>
    void UpdateRemoteTAs(IList<TransportAddress> tas);
  }
}
