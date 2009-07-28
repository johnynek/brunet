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

      serviceInstaller.DisplayName = "GroupVPN";
      serviceInstaller.StartType = ServiceStartMode.Manual;

      serviceInstaller.ServiceName = "GroupVPN";

      this.Installers.Add(serviceProcessInstaller);
      this.Installers.Add(serviceInstaller);
    }
  }
}
