/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Brunet
{
  public class BrunetRpc: IDataHandler 
  {
    protected readonly ReqrepManager _rrm;
    public readonly RpcManager Rpc;
    public readonly IPHandler IPHandler;
    protected readonly Thread _timer;
    protected volatile bool _running;

    public BrunetRpc() {
      _rrm = new ReqrepManager("BrunetRpc");
      _rrm.Subscribe(this, null);
      Rpc = new RpcManager(_rrm);
      IPHandler = new IPHandler();
      IPHandler.Subscribe(this, null);
      _running = true;
      _timer = new Thread(TimerThread);
      _timer.IsBackground = true;
      _timer.Start();
    }

    private void TimerThread() {
      while(_running) {
        _rrm.TimeoutChecker(null, null);
        Thread.Sleep(1000);
      }
    }

    public void Close() {
      _running = false;
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
