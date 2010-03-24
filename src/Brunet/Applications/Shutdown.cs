using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Brunet;

namespace Brunet.Applications {
  /**
  <summary>Provides a clean shutdown path, overloaded members should implement
  ctrl-c handlers.  The ctrl-c handler should call Exit and the nodes should
  add their shutdown method to the OnExit CallBack.</summary>
  */
  public class Shutdown {
    /// <summary>Add the shutdown method to this delegate</summary>
    public event ThreadStart OnExit;
    /**  <summary>This should be called by ctrl-c handlers in inherited classes
    </summary>*/
    public int Exit() {
      if(OnExit != null) {
        new Thread(OnExit).Start();
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
          sd = new WindowsShutdown();
        }
        else {
          sd = new Shutdown();
        }
      }
      catch {
        if(OSDependent.OSVersion == OSDependent.OS.Linux) {
          Console.WriteLine("Shutting down via ctrl-c will not work, perhaps " +
              "you do not have the Mono.Posix libraries installed");
        }
        sd = new Shutdown();
      }
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
    protected void InterruptHandler(int signal) {
      Console.WriteLine("Receiving signal: {0}. Exiting", signal);
      Exit();
    }
  }

  /**
  <summary>Implements a ctrl-c handler for Windows</summary>
  */
  public class WindowsShutdown : Shutdown {
    public enum ConsoleEvent {
     CTRL_C = 0,
     CTRL_BREAK = 1,
     CTRL_CLOSE = 2,
     CTRL_LOGOFF = 5,
     CTRL_SHUTDOWN = 6
    }

    private delegate bool HandlerRoutine(ConsoleEvent console_event);
    HandlerRoutine _shutdown_callback;

    [DllImport("kernel32.dll")]
    static extern int SetConsoleCtrlHandler(HandlerRoutine routine, bool add_not_remove);

    /// <summary>Registers the ctrl-c handler InterruptHandler.</summary>
    public WindowsShutdown() {
      // This is retarded, we have to keep the delegate in memory or it gets
      // GCed and this will throw a rather nasty exception!
      _shutdown_callback = ConsoleEventHandler;
      SetConsoleCtrlHandler(_shutdown_callback, true);
    }

    /**
    <summary>Whenever the user presses ctrl-c this is called by the
    operating system.</summary>
    <param name="signal">The signal number sent to the application</param>
    */
    protected bool ConsoleEventHandler(ConsoleEvent console_event) {
      if(console_event != ConsoleEvent.CTRL_LOGOFF) {
        SetConsoleCtrlHandler(ConsoleEventHandler, false);
        Console.WriteLine("Receiving signal: {0}. Exiting", console_event);
        Exit();
      }
      return true;
    }
  }
}
