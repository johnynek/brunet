using System;
using System.Diagnostics;
using Brunet;

namespace Ipop {
  public abstract class Shutdown {
    public delegate void CallBack();
    public CallBack OnExit;
    public int Exit() {
      if(OnExit != null) {
        OnExit();
      }
      Console.WriteLine("Done!");
      return 0;
    }

    public static Shutdown GetShutdown() {
      Shutdown sd = null;
      if(OSDependent.OSVersion == OSDependent.Linux) {
        sd = new LinuxShutdown();
      }
      else if(OSDependent.OSVersion == OSDependent.Windows) {
        sd = null;
      }
      else {
        throw new Exception("Unknown OS!");
      }
      return sd;
    }
  }

  public class LinuxShutdown : Shutdown {
    public LinuxShutdown() {
      Mono.Unix.Native.Stdlib.signal(Mono.Unix.Native.Signum.SIGINT, new
          Mono.Unix.Native.SignalHandler(InterruptHandler));
    }

    public void InterruptHandler(int signal) {
      Console.WriteLine("Receiving signal: {0}. Exiting", signal);
      Exit();
    }
  }
}
