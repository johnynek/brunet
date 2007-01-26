using System;
using System.Collections;

using Brunet;
using Brunet.Dht;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Ipop {
  public class OSDependent {
// 0 - Linux
// 1 - Windows
    private static int OSVersion;
    public static int OSVers {
      get {
        return OSVersion;
      }
    }
//    private static OSDependent routines;
    public static IEnumerable GetIPAddresses(string [] interfaces) {
      if(OSVersion != 0 || OSVersion != 1)
        DetectOS();
      if(OSVersion == 0)
        return new IPAddressesLinux(interfaces);
      else if(OSVersion == 1)
        return new IPAddressesWindows(interfaces);
      System.Console.WriteLine("Invalid Operating System");
      return null;
    }

// Setup system
    public static void Setup() {
      DetectOS();
    }

// Sets OS
    public static void DetectOS() {
      int p = (int) Environment.OSVersion.Platform;
      if ((p == 4) || (p == 128)) {
        OSVersion = 0;
//        routines = new LinuxOSDependent();
      }
      else {
        OSVersion = 1;
//        routines = new WindowsOSDependent();
      }
    }
  }
}