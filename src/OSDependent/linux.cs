using System;
using System.IO;
using System.Collections;
using System.Net;

namespace Ipop {
  public class OSDependent : OSDependentAbstract {
    public override System.Net.IPAddress[] GetIPTAs(string Virtual_IPAddr) {
      ArrayList tas = new ArrayList();
      try {
	//we make a call to ifconfig here
	ArrayList addr_list = new ArrayList();
	System.Diagnostics.Process proc = new System.Diagnostics.Process();
	proc.EnableRaisingEvents = false;
	proc.StartInfo.RedirectStandardOutput = true;
	proc.StartInfo.UseShellExecute = false;
	proc.StartInfo.FileName = "ifconfig";
	
	proc.Start();
	proc.WaitForExit();
	
	StreamReader sr = proc.StandardOutput;
	while (true) {
	  string output = sr.ReadLine();
	  if (output == null) {
	    break;
	  }
	  output = output.Trim();
	  if (output.StartsWith("inet addr")) {
	    string[] arr = output.Split(' ');
	    if (arr.Length > 1) {
	      string[] s_arr = arr[1].Split(':');
	      if (s_arr.Length > 1) {
		System.Net.IPAddress ip = System.Net.IPAddress.Parse(s_arr[1]);
		Console.WriteLine("Discovering: {0}", ip);
		addr_list.Insert(0, ip);
	      }
	    }
	  }
	}
        foreach(System.Net.IPAddress a in addr_list) {
	  //first and foremost, test if it is a virtual IP
          IPAddress testIp = new IPAddress(a.GetAddressBytes());
          IPAddress temp = IPAddress.Parse(Virtual_IPAddr);
	  if (temp.Equals(testIp)) {
	    Console.WriteLine("Detected {0} as virtual Ip.", Virtual_IPAddr);
	    continue;
	  }
          /**
           * We add Loopback addresses to the back, all others to the front
           * This makes sure non-loopback addresses are listed first.
           */
          if( System.Net.IPAddress.IsLoopback(a) ) {
            //Put it at the back
//            tas.Add(a);  Disabled as a temporary fix / check
          }
          else {
            //Put it at the front
            tas.Insert(0, a);
          }
        }
      }
      catch(Exception x) {
        //If the hostname is not properly configured, we could wind
        //up here.  Just put the loopback address is:
        tas.Add(System.Net.IPAddress.Loopback);
      }
      return (System.Net.IPAddress[]) tas.ToArray(typeof(System.Net.IPAddress));
    }

    public override ArrayList GetNameservers() {
      ArrayList Nameservers = new ArrayList();
      FileStream file = new FileStream("/etc/resolv.conf",
        FileMode.OpenOrCreate, FileAccess.Read);
      StreamReader sr = new StreamReader(file);
      string temp = "", nameserver = "";
      while((temp = sr.ReadLine()) != null) {
        if(temp.StartsWith("nameserver")) {
          nameserver = temp.Substring(11, temp.Length - 11);
          if(nameserver != "127.0.0.1" && nameserver != "0.0.0.0" && nameserver != "")
            Nameservers.Add(nameserver);
        }
      }
      sr.Close();
      file.Close();
      return Nameservers;
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
  }
}