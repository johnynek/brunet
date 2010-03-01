/*
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida
                   Pierre St Juste <ptony82@ufl.edu>, University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;

using Brunet;
using Brunet.Applications;
using Ipop;
using SocialVPN;

namespace WindowsService {
  class WindowsService : ServiceBase {
    object _sync;
    Thread _thread;
    SocialNode _node;

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

      SocialConfig social_config = null;
      NodeConfig node_config = null;
      IpopConfig ipop_config = null;
      string http_port, jabber_port, global_access;

      social_config = Utils.ReadConfig<SocialConfig>("social.config");
      node_config = Utils.ReadConfig<NodeConfig>(social_config.BrunetConfig);
      ipop_config = Utils.ReadConfig<IpopConfig>(social_config.IpopConfig);
      http_port = social_config.HttpPort;
      jabber_port = social_config.JabberPort;
      global_access = social_config.GlobalAccess;

      _node = new SocialNode(node_config, ipop_config, 
                                       node_config.Security.CertificatePath,
                                       http_port, jabber_port, global_access);
      _node.Run();
      //Thread.MemoryBarrier();
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

