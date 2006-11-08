using System;
using System.IO;
using System.Collections;
using System.Net;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Ipop {
  public class OSDependent : OSDependentAbstract {
    public override IPAddress[] GetIPTAs(string [] devices) {
      IPAddress []tas = (IPAddress[]) Array.CreateInstance(typeof(IPAddress), devices.Length);
      for (int i = 0; i < devices.Length; i++)
        tas[i] = GetIPOfIF(devices[i]);
      return tas;
    }

    public override string GetTapAddress(string device) {
      try {
        System.Diagnostics.Process proc = new System.Diagnostics.Process();
        proc.EnableRaisingEvents = false;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.FileName = "ifconfig";
        proc.StartInfo.Arguments = device;
        proc.Start();
        proc.WaitForExit();

        StreamReader sr = proc.StandardOutput;
        sr.ReadLine();
        string output = sr.ReadLine();
        int point1 = output.IndexOf("inet addr:") + 10;
        int point2 = output.IndexOf("Bcast:") - 2 - point1;
        return output.Substring(point1, point2);
      }
      catch (Exception e) {
        return null;
      }
    }

    public override string GetTapNetmask(string device) {
      string result = null;
      System.Diagnostics.Process proc = new System.Diagnostics.Process();
      proc.EnableRaisingEvents = false;
      proc.StartInfo.RedirectStandardOutput = true;
      proc.StartInfo.UseShellExecute = false;
      proc.StartInfo.FileName = "ifconfig";
      proc.StartInfo.Arguments = device;
      proc.Start();
      proc.WaitForExit();

      StreamReader sr = proc.StandardOutput;
      sr.ReadLine();
      string output = sr.ReadLine();
      int point1 = output.IndexOf("Mask:") + 5;
      result = output.Substring(point1, output.Length - point1);
      return result;
    }

    public override void SetHostname(string hostname) {
      System.Diagnostics.Process proc = new System.Diagnostics.Process();
      proc.EnableRaisingEvents = false;
      proc.StartInfo.UseShellExecute = false;
      proc.StartInfo.FileName = "hostname";
      proc.StartInfo.Arguments = hostname;
      proc.Start();
      proc.WaitForExit();
    }

    public override void SetTapDevice(string device, string IPAddress, string Netmask) {
      System.Diagnostics.Process proc = new System.Diagnostics.Process();
      proc.EnableRaisingEvents = false;
      proc.StartInfo.UseShellExecute = false;
      proc.StartInfo.FileName = "ifconfig";
      proc.StartInfo.Arguments = device + " " + IPAddress +
        " netmask " + Netmask;
      proc.Start();
      proc.WaitForExit();
    }

    public override void SetTapMAC(string device) {
      System.Diagnostics.Process proc = new System.Diagnostics.Process();
      proc.EnableRaisingEvents = false;
      proc.StartInfo.UseShellExecute = false;
      proc.StartInfo.FileName = "ifconfig";

      proc.StartInfo.Arguments = device + " down hw ether FE:FD:00:00:00:01";
      proc.Start();
      proc.WaitForExit();

      proc.StartInfo.Arguments = device + " up";
      proc.Start();
      proc.WaitForExit();
    }

    public IPAddress GetIPOfIF(string if_name) {
      ProcessStartInfo cmd = new ProcessStartInfo("/sbin/ifconfig");
      cmd.RedirectStandardOutput = true;
      cmd.UseShellExecute = false;
      Process p = Process.Start(cmd);
      string line = p.StandardOutput.ReadLine();
      //string this_if = null;
      Regex if_line = new Regex(@"^(\S+)\s+Link encap:(.*)$");
      Regex ip_info = new Regex(@"inet addr:(\S+)");
      string this_if = null;
      IPAddress result = null;
      while( line != null ) {
        Match m = if_line.Match(line);
        if( m.Success ) {
          //System.Console.WriteLine(line);
          Group g = m.Groups[1];
          CaptureCollection cc = g.Captures;
          //System.Console.WriteLine(cc[0]);
          this_if = cc[0].ToString();
        }
        m = ip_info.Match(line);
        if( m.Success ) {
          //System.Console.WriteLine(line);
          Group g = m.Groups[1];
          CaptureCollection cc = g.Captures;
          //System.Console.WriteLine(cc[0]);
          if( this_if.Equals( if_name ) ) {
            //We got our interface:
            result = IPAddress.Parse( cc[0].ToString() ); 
            break;
          }
        }
        line = p.StandardOutput.ReadLine();
      }
      return result;
    }
  }
}
