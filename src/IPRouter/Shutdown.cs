using System;
using System.Diagnostics;
using Brunet;

namespace Ipop {
  public abstract class Shutdown {
    public IpopNode _node;
    protected void EndGame() {
      if (_node.Brunet != null) {
        _node.Brunet.Disconnect();
      }
      ProtocolLog.WriteIf(IPOPLog.BaseLog, "Exiting...");
      System.Threading.Thread.Sleep(5000);
      System.Environment.Exit(1);
    }

    public Shutdown(IpopNode node) {
      _node = node;
    }
  }

  public class LinuxShutdown : Shutdown {
    public LinuxShutdown(IpopNode node): base(node) {
      Mono.Unix.Native.Stdlib.signal(Mono.Unix.Native.Signum.SIGINT, new
          Mono.Unix.Native.SignalHandler(InterruptHandler));
    }

    public void InterruptHandler(int signal) {
      ProtocolLog.WriteIf(IPOPLog.BaseLog, String.Format(
        "Receiving signal: {0}. Exiting", signal));
      EndGame();
    }
  }
}
