using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;

namespace Ipop.Dht.Service {
  class WindowsService : ServiceBase {
    protected bool _running;
    protected Thread _thread;
    protected object _lock;

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
      _lock = new object();
      _thread = null;
      _running = false;
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
      lock(_lock) {
        if(_running) {
          return;
        }
        _running = true;
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
      Ipop.Dht.Runner.Main(args);
    }

    protected override void OnStop()
    {
      Brunet.Applications.BasicNode current_node = null;
      Thread thread = null;

      lock(_lock) {
        if(!_running) {
          return;
        }
        _running = false;
        thread = _thread;
        _thread = null;
        current_node = Ipop.Dht.Runner.CurrentNode;
      }

      if(thread == null || current_node == null) {
        return;
      }

      current_node.Shutdown.Exit();
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

