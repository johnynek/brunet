using System;
using System.Collections;

namespace Ipop {
  public class OSDependent {
    public static int Linux {get { return 0; } }
    public static int Windows {get { return 1; } }
    private static int OSVersion;
    public static int OSVers {
      get {
        return OSVersion;
      }
    }
//    private static OSDependent routines;
    public static IEnumerable GetIPAddresses(string [] interfaces) {
      if(OSVers == Linux)
        return new IPAddressesLinux(interfaces);
      else if(OSVers == Windows)
        return new IPAddressesWindows(interfaces);
      System.Console.Error.WriteLine("Invalid Operating System");
      return null;
    }

    public static IEnumerable GetIPAddresses() {
      if(OSVers == Linux)
        return new IPAddressesLinux();
      else if(OSVers == Windows)
        return new IPAddressesWindows();
      System.Console.Error.WriteLine("Invalid Operating System");
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
        OSVersion = Linux;
//        LinuxRoutines.Setup();
      }
      else {
        OSVersion = Windows;
//        routines = new WindowsOSDependent();
      }
    }
  }
}