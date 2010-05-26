using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;

namespace Ipop.Dht.Service {
  class WindowsService : ServiceBase {
    Thread _thread;

    public WindowsService()
    {
      ServiceName = "GroupVPN";
      EventLog.Log = "Application";

      CanHandlePowerEvent = false;
      CanHandleSessionChangeEvent = true;
      CanPauseAndContinue = false;
      CanShutdown = true;
      CanStop = true;
      AutoLog = true;
    }

    public static void Main()
    {
      ServiceBase.Run(new WindowsService());
    }

    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
    }

    protected override void OnStart(string[] args)
    {
      Thread thread = new Thread(Start);
      if(Interlocked.CompareExchange<Thread>(ref _thread, null, thread) != null) {
        return;
      }

      _thread.Start();
      base.OnStart(args);
    }

    protected virtual void Start()
    {

      string[] args = new string[] {"-d", "dhcp.config", "-i", "ipop.config",
        "-n", "node.config"};

      string app_dir = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
      System.IO.Directory.SetCurrentDirectory(app_dir);
      Ipop.Dht.Runner.Main(args);
      Thread.MemoryBarrier();
    }

    protected override void OnStop()
    {
      Thread thread = Interlocked.Exchange(ref _thread, null);
      if(thread == null) {
        return;
      }
      Ipop.Dht.Runner.CurrentNode.Shutdown.Exit();
      thread.Join();
      base.OnStop();
    }

    protected override void OnPause()
    {
      base.OnPause();
    }

    protected override void OnContinue()
    {
      base.OnContinue();
    }

    protected override void OnShutdown()
    {
      base.OnShutdown();
    }

    protected override void OnCustomCommand(int command)
    {
      base.OnCustomCommand(command);
    }

    protected override bool OnPowerEvent(PowerBroadcastStatus status)
    {
      return base.OnPowerEvent(status);
    }

    protected override void OnSessionChange(SessionChangeDescription description)
    {
      base.OnSessionChange(description);
    }
  }
}

