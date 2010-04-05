/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

namespace Brunet.Applications {
  /**
  This class retrieves all the IP Address information on the local machine.
  Most importantly the device name and the IP Address associated with it.
  */
  public abstract class IPAddresses : IEnumerable {
    /**  <summary>A list of the interfaces to look up when GetAddresses is
    called</summary>*/
    protected ArrayList _ints;
    /// <summary>Contains interfaces information</summary>
    public readonly IList AllInterfaces;

    /// <summary>Used to look up all interfaces</summary>
    public IPAddresses():this(null) {}

    /**
    <summary>Used to look up specific list of interfaces.  This
    should not be called directly use GetIPAddresses(string[] interfaces)
    </summary>
    <param name="interfaces">A list of interfaces to look up</param>
    */
    public IPAddresses(string[] interfaces) {
      _ints = null;
      if(interfaces != null) {
        _ints = new ArrayList(interfaces);
      }
      // Remove this later...
      AllInterfaces = GetAddresses();
    }

    /**
    <summary>Automatically chooses which version of IPAddresses to instantiate.
    This version looks up all the interfaces for the host</summary>
    <returns>An IPAddresses for the host.</returns>
    */
    public static IPAddresses GetIPAddresses() {
      return GetIPAddresses(null);
    }

    /**
    <summary>Automatically chooses which version of IPAddresses to instantiate.
    This version of the constructor allows you to choose which interfaces to
    look up</summary>
    <param name="interfaces">An array of interfaces to look up,</param>
    <returns>An IPAddresses for the host.</returns>
    */
    public static IPAddresses GetIPAddresses(string[] interfaces) {
      IPAddresses ipaddrs = null;
      if(OSDependent.OSVersion == OSDependent.OS.Linux) {
        ipaddrs = new IPAddressesLinux(interfaces);
      }
      else if(OSDependent.OSVersion == OSDependent.OS.Windows) {
        ipaddrs = new IPAddressesWindows(interfaces);
      }
      else {
        throw new Exception("Unknown OS!");
      }
      return ipaddrs;
    }

    /**
    <summary>To be of type IEnumerable, here is the Enumerator.  This
    enumerates through the IList given by GetIPAddresses</summary>
    */
    public IEnumerator GetEnumerator() {
      //IList all_interfaces = GetAddresses();
      foreach(Hashtable ht in AllInterfaces) {
        if( ht.ContainsKey("interface") && ht.ContainsKey("inet addr") ) {
          string iface = (string)ht["interface"];
          if( _ints == null || _ints.Count == 0 ) {
            yield return IPAddress.Parse( (string)ht["inet addr"] );
          }
          else if( _ints.Contains(iface) ) {
            yield return IPAddress.Parse( (string)ht["inet addr"] );
          }
        }
      }
    }

    /**
    <summary>This is the system depedent part of the code.  It should call
    look up address information and place it into an IList.</summary>
    <returns>An IList containing hashtables containing network device
    information.</returns>
    */
    public abstract IList GetAddresses();

    /**
    <summary>Called by GetAddresses to add the value to the correct place in the 
    hashtable.</summary>
    <param name="re">The regular expression to match.</param>
    <param name="line">The line to check.</param>
    <param name="ht">The hashtable to store the data if their is a match.
    </param>
    <param name="key">Position in the key in the hashtable to store the result
    </param>
    <returns>True if a match</returns>
     */
    protected bool AddIfMatch(Regex re, string line, Hashtable ht, string key) {
      Match m = re.Match(line);
      if( m.Success ) {
        Group g = m.Groups[m.Groups.Count - 1];
        CaptureCollection cc = g.Captures;
        ht[key] = cc[0].ToString();
        return true;
      }
      return false;
    }

    /// <summary>Prints the IPAddresses IList to the console.</summary>
    public void Print() {
      System.Console.Error.WriteLine("Network list:\n");
      foreach(Hashtable ht in AllInterfaces) {
        IDictionaryEnumerator en = ht.GetEnumerator();
        while(en.MoveNext()) {
          System.Console.Error.WriteLine("\t{0}: {1}", en.Key, en.Value);
        }
        System.Console.Error.WriteLine();
      }
    }
  }

  /// <summary>Linux specific implementations of IPAddresses.</summary>
  public class IPAddressesLinux : IPAddresses {
    /**
    <summary>Used to look up specific list of interfaces.  This
    should not be called directly use GetIPAddresses(string[] interfaces)
    </summary>
    <param name="interfaces">A list of interfaces to look up</param>
     */
    public IPAddressesLinux (string [] interfaces) : base(interfaces) {}
    /// <summary>Used to look up all interfaces</summary>
    public IPAddressesLinux() : base() {}

    /**
    <summary>Implements GetAddresses for Linux which calls ifconfig.</summary>
    <returns>An IList containing hashtables containing network device
    information.</returns>
    */
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
      keys["HWaddr"] = new Regex(@"HWaddr ([0-9A-Fa-f:]+)");
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

  /// <summary>Windows specific implementations of IPAddresses.</summary>
  public class IPAddressesWindows : IPAddresses {
    /**
    <summary>Used to look up specific list of interfaces.  This
    should not be called directly use GetIPAddresses(string[] interfaces)
    </summary>
    <param name="interfaces">A list of interfaces to look up</param>
    */
    public IPAddressesWindows (string [] interfaces) : base(interfaces) {}

    /// <summary>Used to look up all interfaces</summary>
    public IPAddressesWindows() : base() {}

    /**
    <summary>Implements GetAddresses for Windows which calls ipconfig.</summary>
    <returns>An IList containing hashtables containing network device
    information.</returns>
    */
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
      keys["inet addr"] = new Regex(@"IP(|v4) Address[\. ]+: ([0-9\.]+)");
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
