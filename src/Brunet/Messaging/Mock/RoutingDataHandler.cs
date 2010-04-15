/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2008 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet;
using Brunet.Util;
using System;
using System.Collections;

#if BRUNET_NUNIT
using NUnit.Framework;
using System.Security.Cryptography;
#endif

namespace Brunet.Messaging.Mock {
  /// <summary>This class provides a RoutingDataHandler object that provides a
  /// filter to remove PTypes and route to subscribed IDataHandler.</summary>
  public class RoutingDataHandler: SimpleSource, IDataHandler {
    public RoutingDataHandler() {
    }

    public void HandleData(MemBlock payload, ISender return_path, object state) {
      if(_sub != null) {
        MemBlock rest = null;
        PType.Parse(payload, out rest);
        _sub.Handle(rest, return_path);
      }
    }
  }
}
