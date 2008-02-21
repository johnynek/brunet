/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

namespace Ipop {
  public abstract class IPAddresses : IEnumerable {
    protected ArrayList _ints;
    protected IList _all_interfaces;
    public IList AllInterfaces {
      get { return _all_interfaces; }
    }

    /**
     * Get all the IPAddresses
     */
    public IPAddresses():this(null) {}

    public IPAddresses(string[] interfaces) {
      _ints = null;
      if(interfaces != null) {
        _ints = new ArrayList(interfaces);
      }
      // Remove this later...
      _all_interfaces = GetAddresses();
    }

    public static IPAddresses GetIPAddresses() {
      return GetIPAddresses(null);
    }

    public static IPAddresses GetIPAddresses(string[] interfaces) {
      IPAddresses ipaddrs = null;
      if(OSDependent.OSVersion == OSDependent.Linux) {
        ipaddrs = new IPAddressesLinux(interfaces);
      }
      else if(OSDependent.OSVersion == OSDependent.Windows) {
        ipaddrs = new IPAddressesWindows(interfaces);
      }
      else {
        throw new Exception("Unknown OS!");
      }
      return ipaddrs;
    }

    /**
     * This is for IEnumerable/foreach support
     * 
     */
    public IEnumerator GetEnumerator() {
      //IList all_interfaces = GetAddresses();
      foreach(Hashtable ht in _all_interfaces) {
        if( ht.ContainsKey("interface") && ht.ContainsKey("inet addr") ) {
          string iface = (string)ht["interface"];
          if( _ints == null ) {
            yield return IPAddress.Parse( (string)ht["inet addr"] );
          }
          else if( _ints.Contains(iface) ) {
            yield return IPAddress.Parse( (string)ht["inet addr"] );
          }
        }
      }
    }

    public abstract IList GetAddresses();

    protected bool AddIfMatch(Regex re, string line, Hashtable ht, string key) {
      Match m = re.Match(line);
      if( m.Success ) {
        Group g = m.Groups[1];
        CaptureCollection cc = g.Captures;
        ht[key] = cc[0].ToString();
        return true;
      }
      return false;
    }

    public void Print() {
      System.Console.Error.WriteLine("Network list:\n");
      foreach(Hashtable ht in _all_interfaces) {
        IDictionaryEnumerator en = ht.GetEnumerator();
        while(en.MoveNext()) {
          System.Console.Error.WriteLine("\t{0}: {1}", en.Key, en.Value);
        }
        System.Console.Error.WriteLine();
      }
    }
  }

  public class IPAddressesLinux : IPAddresses {
    public IPAddressesLinux (string [] interfaces) : base(interfaces) {}
    public IPAddressesLinux() : base() {}

    public override IList GetAddresses() {
      ProcessStartInfo cmd = new ProcessStartInfo("/sbin/ifconfig");
      cmd.RedirectStandardOutput = true;
      cmd.UseShellExecute = false;
      Process p = Process.Start(cmd);

      string line = p.StandardOutput.ReadLine();
      //string this_if = null;
      Regex if_line = new Regex(@"^(\S+)\s+Link encap:");

      Hashtable keys = new Hashtable();
      keys["inet addr"] = new Regex(@"inet addr:([0-9\.]+)");
      keys["Bcast"] = new Regex(@"Bcast:([0-9\.]+)");
      keys["Mask"] = new Regex(@"Mask:([0-9\.]+)");
      keys["HWaddr"] = new Regex(@"HWaddr ([0-9A-F:]+)");
      keys["MTU"] = new Regex(@"MTU:([0-9]+)");

      ArrayList result = new ArrayList();
      Hashtable entry = null;
      while( line != null ) {
        //Get the interface:
        Match m = if_line.Match(line);
        if( m.Success ) {
        /*
         * This is a new Interface, add the old one to the list:
        */
          if( entry != null ) {
            result.Add(entry);
          }
          entry = new Hashtable();
          //System.Console.Error.WriteLine(line);
          Group g = m.Groups[1];
          CaptureCollection cc = g.Captures;
          //System.Console.Error.WriteLine(cc[0]);
          entry["interface"] = cc[0].ToString();
        }
        IDictionaryEnumerator key_en = keys.GetEnumerator();
        while(key_en.MoveNext() ) {
          AddIfMatch((Regex)key_en.Value, line, entry, (string)key_en.Key);
        }
        line = p.StandardOutput.ReadLine();
      }
      if( entry != null ) {
        result.Add(entry);
      }
      p.Close();
      return result;
    }
  }

  public class IPAddressesWindows : IPAddresses {
    public IPAddressesWindows (string [] interfaces) : base(interfaces) {}
    public IPAddressesWindows() : base() {}

    public override IList GetAddresses() {
      String root = Environment.GetEnvironmentVariable("SystemRoot");
      ProcessStartInfo cmd = new
          ProcessStartInfo(root + "\\system32\\ipconfig.exe");
      cmd.Arguments = "/all";
      cmd.RedirectStandardOutput = true;
      cmd.UseShellExecute = false;
      Process p = Process.Start(cmd);

      string line = p.StandardOutput.ReadLine();
      //string this_if = null;
      Regex if_line = new Regex(@"\S+ adapter ([^:]+)");

      Hashtable keys = new Hashtable();
      keys["inet addr"] = new Regex(@"IP Address. . . . . . . . . . . . : ([0-9\.]+)");
      keys["Bcast"] = new Regex(@"DOES NOT EXIST");
      keys["Mask"] = new Regex(@"Subnet Mask . . . . . . . . . . . : ([0-9\.]+)");
      keys["HWaddr"] = new Regex(@"Physical Address. . . . . . . . . : ([0-9A-F\-]+)");
      keys["MTU"] = new Regex(@"DOES NOT EXIST");

      ArrayList result = new ArrayList();
      Hashtable entry = null;
      while( line != null ) {
        //Get the interface:
        Match m = if_line.Match(line);
        if( m.Success ) {
        /*
         * This is a new Interface, add the old one to the list:
        */
          if( entry != null ) {
            result.Add(entry);
          }
          entry = new Hashtable();
          //System.Console.Error.WriteLine(line);
          Group g = m.Groups[1];
          CaptureCollection cc = g.Captures;
          //System.Console.Error.WriteLine(cc[0]);
          entry["interface"] = cc[0].ToString();
        }
        IDictionaryEnumerator key_en = keys.GetEnumerator();
        while(key_en.MoveNext() ) {
          AddIfMatch((Regex)key_en.Value, line, entry, (string)key_en.Key);
        }
        line = p.StandardOutput.ReadLine();
      }
      if( entry != null ) {
        result.Add(entry);
      }
      p.Close();
      return result;
    }
  }
}
