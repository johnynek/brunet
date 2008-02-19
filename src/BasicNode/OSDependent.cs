using System;
using System.Collections;

namespace Ipop {
  public class OSDependent {
    public static int Linux {get { return 0; } }
    public static int Windows {get { return 1; } }
    private static int _osver;
    public static int OSVersion {
      get {
        return _osver;
      }
    }

    static OSDependent() {
      int p = (int) Environment.OSVersion.Platform;
      if ((p == 4) || (p == 128)) {
        _osver = Linux;
      }
      else {
        _osver = Windows;
      }
    }
  }
}