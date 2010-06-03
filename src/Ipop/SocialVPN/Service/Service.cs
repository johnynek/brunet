/*
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida
                   Pierre St Juste <ptony82@ufl.edu>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;

namespace Ipop.SocialVPN.Service {
  class WindowsService : ServiceBase {
    object _sync;
    Thread _thread;
    Ipop.SocialVPN.SocialNode _node;

    public WindowsService()
    {
      ServiceName = "SocialVPN";
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
      string app_dir = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
      System.IO.Directory.SetCurrentDirectory(app_dir);

      _node = Ipop.SocialVPN.SocialNode.CreateNode();
      _node.Run();
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

      _node.Shutdown.Exit();
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

