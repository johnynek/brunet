using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;

namespace WindowsService {
  class WindowsService : ServiceBase {
    object _sync;
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
      _sync = new object();
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
      lock(_sync) {
        _thread = new Thread(Start);
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
      Ipop.DhtNode.Runner.Main(args);
      Thread.MemoryBarrier();
    }

    protected override void OnStop()
    {
      Thread thread = null;
      lock(_sync) {
        thread = _thread;
        _thread = null;
      }
      if(thread == null) {
        return;
      }

      Ipop.DhtNode.Runner.CurrentNode.Shutdown.Exit();
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

    protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
    {
      return base.OnPowerEvent(powerStatus);
    }

    protected override void OnSessionChange(
              SessionChangeDescription changeDescription)
    {
      base.OnSessionChange(changeDescription);
    }
  }
}

