using System;

namespace Brunet {
  public class OS {
    static int Linux = 0;
    static int Windows = 1;
    // 0 - Linux
    // 1 - Windows
    public static int OSVersion() {
      int ver = (int) Environment.OSVersion.Platform;
      if(ver == 128 || ver == 4) {
        return Linux;
      }
      else {
        return Windows;
      }
    }

    public static string OSDir() {
      if(OSVersion() == Linux) {
        return "/";
      }
      else {
        return "\\";
      }
    }
  }
}

