using System.Diagnostics;
using System.Threading;

namespace Brunet.Util {
  public class ProtocolLog {
    private static object _sync = new object();
    public static BooleanSwitch ConsoleLogEnable =
        new BooleanSwitch("ConsoleLogEnable", "Log for unknown!");
    public static BooleanSwitch Connections =
      new BooleanSwitch("ConnectionTable", "Logs connections");
    public static BooleanSwitch ConnectionTableLocks = 
        new BooleanSwitch("ConnectionTableLocks", "Logs locks in the ConnectionTable");
    public static BooleanSwitch Exceptions =
        new BooleanSwitch("ERROR", "Logs exceptions");
    public static BooleanSwitch NodeLog =
        new BooleanSwitch("Node", "Log for node");
    public static BooleanSwitch AnnounceLog =
        new BooleanSwitch("Announce", "Log for AnnounceThread");
    public static BooleanSwitch UdpEdge =
        new BooleanSwitch("UdpEdge", "Log for UdpEdge and UdpEdgeListener");
    public static BooleanSwitch NatHandler =
        new BooleanSwitch("NatHandler", "Log for NatHandler");
    public static BooleanSwitch SCO =
        new BooleanSwitch("SCO", "Log for SCO");
    public static BooleanSwitch Stats =
        new BooleanSwitch("Stats", "Log for stats of the system");
    public static BooleanSwitch LinkDebug =
        new BooleanSwitch("LinkDebug", "Log for Link");
    public static BooleanSwitch RelayEdge =
        new BooleanSwitch("RelayEdge", "Log for RelayEdge");
    public static BooleanSwitch LPS =
        new BooleanSwitch("LPS", "Log for link protocol state");
    public static BooleanSwitch Monitor =
        new BooleanSwitch("Monitor", "Log the system monitor");
    public static BooleanSwitch LocalCO =
        new BooleanSwitch("LocalCO", "Log the local connection overlord");
    public static BooleanSwitch EdgeClose =
        new BooleanSwitch("EdgeClose", "The reason why an edge was closed.");
    public static BooleanSwitch MapReduce =
        new BooleanSwitch("MapReduce", "Log map-reduce computations");
    public static BooleanSwitch ManagedCO =
        new BooleanSwitch("ManagedCO", "User selected connections.");
    public static BooleanSwitch Security =
        new BooleanSwitch("Security", "Security logging.");
    public static BooleanSwitch SecurityExceptions =
        new BooleanSwitch("SecurityExceptions", "Security Handling Exception logging.");
    public static BooleanSwitch Pathing =
        new BooleanSwitch("Pathing", "Log for pathing");

    public static bool CTL_enabled = false;

    public static void Enable() {
      if(!ConsoleLogEnable.Enabled) {
        return;
      }
      lock(_sync) {
        if(!CTL_enabled) {
          Trace.Listeners.Add(new ConsoleTraceListener(true));
          CTL_enabled = true;
        }
      }
    }

    /**
     * According to documentation using the other WriteIf actually calls write.
     * I don't know if that really makes much sense, but as a safe keep, let's
     * use this WriteIf, shame there is no C# inlining :(
     */
    public static void WriteIf(BooleanSwitch bs, string msg) {
#if TRACE
      if(bs.Enabled) {
        Trace.WriteLine(bs.DisplayName + ":  " + Thread.CurrentThread.Name + ":  " + msg);
      }
#elif BRUNET_NUNIT
      if(bs.Enabled) {
        System.Console.WriteLine(msg);
      }
#endif
    }

    public static void Write(BooleanSwitch bs, string msg) {
#if TRACE
      Trace.WriteLine(bs.DisplayName + ":  " + Thread.CurrentThread.Name + ":  " + msg);
#elif BRUNET_NUNIT
      System.Console.WriteLine(msg);
#endif
    }
  }
}
