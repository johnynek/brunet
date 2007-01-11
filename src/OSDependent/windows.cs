/* Not Implemented Yet */

using System;
using System.IO;

namespace Ipop {
  class OSDependent : OSDependentoverride static {
/*  If any exceptions occur in these, excepted listed below, the methods 
    should exit the program w/ an exception */
    public override static ArrayList GetNameservers();
/*  Returns null if none set yet, not an exception */
    public override static string GetTapAddress(string device);
    public override static string GetTapNetmask(string device);
    public override static void SetHostname(string hostname);
    public override static void SetTapDevice(string device, string IPAddress, string Netmask);
    public override static void SetTapMAC(string device);
  }
}