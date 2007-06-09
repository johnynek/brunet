using System;

namespace Ipop {
  public abstract class Shutdown {
    protected void EndGame() {
      if (IPRouter.node.brunet != null) {
//        IPRouter.node.brunet.InterruptRefresher();
        IPRouter.node.brunet.Disconnect();
      }
      System.Console.Error.WriteLine("Exiting....");
      System.Threading.Thread.Sleep(5000);
      System.Environment.Exit(1);
    }
  }

  public class LinuxShutdown : Shutdown {
    public LinuxShutdown() {
      Mono.Unix.Native.Stdlib.signal(Mono.Unix.Native.Signum.SIGINT, new
          Mono.Unix.Native.SignalHandler(InterruptHandler));
    }

    public void InterruptHandler(int signal) {
      Console.Error.WriteLine("Receiving signal: {0}. Exiting", signal);
      EndGame();
    }
  }
}
