using System;
using System.IO;
using System.Collections;
using System.Threading;

namespace Ipop {
  public abstract class OSDependentAbstract {
    private string TapDeviceName;
/*  If any exceptions occur in these, excepted listed below, the methods 
    should exit the program w/ an exception */
/*  Have a try / catch in case the hostname isn't configured properly */
    public abstract System.Net.IPAddress[] GetIPTAs(string Virtual_IPAddr);
    public abstract ArrayList GetNameservers();
/*  Returns null if none set yet, not an exception */
    public abstract string GetTapAddress(string device);
    public abstract string GetTapMAC(string device);
    public abstract string GetTapNetmask(string device);
    public abstract void SetHostname(string hostname);
    public abstract void SetRouteAndArp(string device, string IPAddress, string Netmask);
    public abstract void SetTapDevice(string device, string IPAddress, string Netmask);

    public string DHCPGetHostname(string IPAddress) {
      string hostname = "C";
      byte [] IP = DHCPCommon.StringToBytes(IPAddress, '.');
      for(int i = 1; i < 4; i++) {
        if(IP[i] < 10)
          hostname += "00";
       else if(IP[i] < 100)
          hostname += "0";
        hostname += IP[i];
      }
      return hostname;
    }

    public void DHCPSetRouteAndArp() {
      while(this.GetTapAddress(TapDeviceName) == null) ;
        Thread.Sleep(1000);
      this.SetRouteAndArp(TapDeviceName,
        this.GetTapAddress(TapDeviceName),
        this.GetTapNetmask(TapDeviceName));
    }

    public void DHCPSetTapDeviceName(string name) {
      TapDeviceName = name;
    }
  }
}