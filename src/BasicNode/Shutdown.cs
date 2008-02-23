using System;
using System.Diagnostics;
using Brunet;

namespace Brunet.Applications {
  /**
   * Provides a clean shutdown path via ctrl-c
   */
  public abstract class Shutdown {
    public delegate void CallBack();
    public CallBack OnExit; /**< Add your callback to this delegate!*/
    public int Exit() {
      if(OnExit != null) {
        OnExit();
      }
      Console.WriteLine("Done!");
      return 0;
    }

    /**
     * Sets up a proper Shutdown for the given system if one exists.  Call
     * this method rather than attempting to setup your own.
     * @return The native shutdown class is returned.
     */
    public static Shutdown GetShutdown() {
      Shutdown sd = null;
      try {
        if(OSDependent.OSVersion == OSDependent.Linux) {
          sd = new LinuxShutdown();
        }
        else if(OSDependent.OSVersion == OSDependent.Windows) {
          sd = null;
        }
        else {
          throw new Exception("Unknown OS!");
        }
      }
      catch {}
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
