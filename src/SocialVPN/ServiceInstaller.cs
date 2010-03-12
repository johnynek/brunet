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
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace WindowsService {
  [RunInstaller(true)]
  public class WindowsServiceInstaller : Installer {
    public WindowsServiceInstaller()
    {
      ServiceProcessInstaller serviceProcessInstaller = new ServiceProcessInstaller();
      ServiceInstaller serviceInstaller = new ServiceInstaller();

      serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
      serviceProcessInstaller.Username = null;
      serviceProcessInstaller.Password = null;

      serviceInstaller.DisplayName = "SocialVPN";
      serviceInstaller.StartType = ServiceStartMode.Manual;

      serviceInstaller.ServiceName = "SocialVPN";

      this.Installers.Add(serviceProcessInstaller);
      this.Installers.Add(serviceInstaller);
    }
  }
}
