/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Brunet.Util;

namespace Brunet.Messaging
{
  public class BrunetRpc: IDataHandler 
  {
    protected readonly ReqrepManager _rrm;
    public readonly RpcManager Rpc;
    public readonly IPHandler IPHandler;
    protected readonly Thread _timer;
    protected int _running;

    public BrunetRpc() {
      _rrm = new ReqrepManager("BrunetRpc");
      _rrm.Subscribe(this, null);
      Rpc = new RpcManager(_rrm);
      IPHandler = new IPHandler();
      IPHandler.Subscribe(this, null);
      _running = 1;
      _timer = new Thread(TimerThread);
      _timer.IsBackground = true;
      _timer.Start();
    }

    private void TimerThread() {
      while(1 == _running) {
        _rrm.TimeoutChecker(null, null);
        Thread.Sleep(1000);
      }
    }

    public void Close() {
      Interlocked.Exchange(ref _running, 0);
      _timer.Join();
      IPHandler.Stop();
    }

    public void HandleData(MemBlock b, ISender from, object state) {
      MemBlock payload = null;
      PType t = null;
      try {
        t = PType.Parse(b, out payload);
        if(t.Equals(PType.Protocol.ReqRep)) {
          _rrm.HandleData(payload, from, state);
        }
        else if(t.Equals(PType.Protocol.Rpc)) {
          Rpc.HandleData(payload, from, state);
        }
      }
      catch(Exception x) {
        Console.Error.WriteLine("Packet Handling Exception: {3}\n\tType: {0}\n\t\n\tFrom: {1}\n\tData: {2}",
          t, from, payload.GetString(System.Text.Encoding.ASCII), x);
      }
    }
  }
}
