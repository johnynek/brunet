using System;
using System.Diagnostics;
using Brunet;

namespace Brunet.Applications {
  /**
  <summary>Provides a clean shutdown path, overloaded members should implement
  ctrl-c handlers.  The ctrl-c handler should call Exit and the nodes should
  add their shutdown method to the OnExit CallBack.</summary>
  */
  public abstract class Shutdown {
    /// <summary>Defines a simple delegate callback for Exiting</summary>
    public delegate void CallBack();
    /// <summary>Add the shutdown method to this delegate</summary>
    public CallBack OnExit;
    /**  <summary>This should be called by ctrl-c handlers in inherited classes
    </summary>*/
    public int Exit() {
      if(OnExit != null) {
        OnExit();
      }
      Console.WriteLine("Done!");
      return 0;
    }

    /**
    <summary>Sets up a proper Shutdown for the given system if one exists.  Call
    this method rather than attempting to setup your own.</summary>
    <returns>The native shutdown class is returned.</returns>
    */
    public static Shutdown GetShutdown() {
      Shutdown sd = null;
      try {
        if(OSDependent.OSVersion == OSDependent.OS.Linux) {
          sd = new LinuxShutdown();
        }
        else if(OSDependent.OSVersion == OSDependent.OS.Windows) {
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

  /**
  <summary>Implements a ctrl-c handler for Linux</summary>
  */
  public class LinuxShutdown : Shutdown {
    /// <summary>Registers the ctrl-c handler InterruptHandler.</summary>
    public LinuxShutdown() {
      Mono.Unix.Native.Stdlib.signal(Mono.Unix.Native.Signum.SIGINT, new
          Mono.Unix.Native.SignalHandler(InterruptHandler));
    }

    /**
    <summary>Whenever the user presses ctrl-c this is called by the
    operating system.</summary>
    <param name="signal">The signal number sent to the application</param>
    */
    public void InterruptHandler(int signal) {
      Console.WriteLine("Receiving signal: {0}. Exiting", signal);
      Exit();
    }
  }
}
