using System;
using System.Net;

namespace Ipop {
  interface OSDependentInterface {
/*  If any exceptions occur in these, excepted listed below, the methods 
    should exit the program w/ an exception */
/*  Have a try / catch in case the hostname isn't configured properly */
    System.Net.IPAddress[] GetIPTAs(string [] devices);
/*  Returns null if none set yet, not an exception */
    string GetTapAddress(string device);
    string GetTapNetmask(string device);
    void SetHostname(string hostname);
    void SetTapDevice(string device, string IPAddress, string Netmask);
    void SetTapMAC(string device);
    IPAddress GetIPOfIF(string if_name);
  }
}