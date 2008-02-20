using System;
using System.Diagnostics;
using Brunet;

namespace Ipop {
  public abstract class Shutdown {
    protected StructuredNode _node;
    public delegate void CallBack();
    public CallBack PreDisconnect;
    public CallBack PostDisconnect;
    public int Exit() {
      if(PreDisconnect != null) {
        PreDisconnect();
      }
      try {
        _node.DisconnectOnOverload = false;
        _node.Disconnect();
      }
      catch {}
      if(PostDisconnect != null) {
        PostDisconnect();
      }
      return 0;
    }

    public Shutdown(StructuredNode node) {
      _node = node;
    }
  }

  public class LinuxShutdown : Shutdown {
    public LinuxShutdown(StructuredNode node): base(node) {
      Mono.Unix.Native.Stdlib.signal(Mono.Unix.Native.Signum.SIGINT, new
          Mono.Unix.Native.SignalHandler(InterruptHandler));
    }

    public void InterruptHandler(int signal) {
      Console.WriteLine("Receiving signal: {0}. Exiting", signal);
      Exit();
    }
  }
}
