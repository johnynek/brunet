/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida
Copyright (C) 2009  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Xml;
using System.Xml.Serialization;
using System.IO;

namespace Ipop {
  /// <summary>A Configuration class for the DHCP Lease Controller</summary>
  public class DHCPConfig {
    /// <summary>The length of a lease.</summary>
    public int LeaseTime;
    /// <summary>The name of the IPOP Namespace.</summary>
    public string Namespace;
    /// <summary>The netmask for the IPOP Namespace.</summary>
    public string Netmask;
    /// <summary>Base IP Address.</summary>
    public string IPBase;
    /// <summary>An array of reserved IP Addresses.</summary>
    public ReservedIP[] ReservedIPs;

    /// <summary>Specifies reserved IP cases</summary>
    public class ReservedIP {
      /// <summary>The address to reserve.</summary>
      public string IPBase;
      /// <summary>The mask to use.</summary>
      public string Mask;
    }
  }
}
