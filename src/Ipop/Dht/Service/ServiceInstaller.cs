using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace Ipop.Dht.Service {
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

      Installers.Add(serviceProcessInstaller);
      Installers.Add(serviceInstaller);
    }
  }
}
