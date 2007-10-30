/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 David Wolinsky <davidiw@ufl.edu>

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
using System.Collections.Generic;

namespace Brunet {

  /**
   * This class provides a single way for all monitoring objects to work
   * together.
   */

  public class GlobalMonitor {
    protected Node _node;
    protected static int sleep_time = 60 * 1000;
    protected static int MAXIMUM_SLEEP_TIME = 30 * 60 * 60 * 1000;
    protected static int MINIMUM_SLEEP_TIME = 60 * 1000;
    public event EventHandler WakeUpEvent;
    protected object _sync;
    protected bool in_sleep;

    public GlobalMonitor(Node node) {
      _node = node;
      in_sleep = false;
      _sync = new object();
    }

    public void Sleep() {
      lock(_sync) {
        if(in_sleep)
          return;
        in_sleep = true;
      }

      if(ProtocolLog.Monitor.Enabled)
        ProtocolLog.Write(ProtocolLog.Monitor, "Sleeping for "
          + sleep_time / 1000 + " seconds");
      _node.sleep_mode = true;
      System.Threading.Thread.Sleep(sleep_time);
      if(WakeUpEvent != null)
        WakeUpEvent(this, EventArgs.Empty);
      _node.sleep_mode = false;

      if(ProtocolLog.Monitor.Enabled)
        ProtocolLog.Write(ProtocolLog.Monitor, "Sleeping over");
      if(sleep_time < MAXIMUM_SLEEP_TIME)
        sleep_time *= 2;
      in_sleep = false;
    }
  }

  /**
   * A small calss to assist in the monitoring of the HeartBeatEvent.  Keeps 
   * track of arrival and service time as well as missed calls.
   */

  public class HeartBeatMonitor {
    protected class HeartBeatData {
      public float arrival_time, service_time;

      public HeartBeatData(DateTime last_arrival, DateTime arrival) {
        service_time = (float) (DateTime.UtcNow - arrival).TotalSeconds;
        arrival_time = (float) (arrival - last_arrival).TotalSeconds;
      }
    }

    protected class HeartBeatHistory: CacheLinkedList<HeartBeatData> {
      public HeartBeatHistory(HeartBeatHistory hbh, HeartBeatData hbd) :
        base(hbh, hbd){}
      public static new int MAX_COUNT = 64;
      public static HeartBeatHistory operator + (HeartBeatHistory hbh,
        HeartBeatData hbd) {
        return new HeartBeatHistory(hbh, hbd);
      }
    }

    protected DateTime _last_arrival;
    protected CacheLinkedList<HeartBeatData> _hbds;
    protected int _lost;
    protected object _sync;
    protected GlobalMonitor _monitor;

    public HeartBeatMonitor(GlobalMonitor monitor) {
      _monitor = monitor;
      _monitor.WakeUpEvent += this.WakeUpCallBack;
      _last_arrival = DateTime.UtcNow;
      _lost = 0;
    }

    public void Add(DateTime arrival) {
      _hbds += new HeartBeatData(_last_arrival, arrival);
      _last_arrival = arrival;
      _sync = new object();
    }

    public void Lost() {
      lock(_sync) {
        _lost++;
      }
    }

    public void CheckSystem() {
      if(_hbds == null)
        return;
      float arrivals = 0, services = 0;
      int count = 0;
      foreach(HeartBeatData hbd in _hbds) {
        arrivals += hbd.arrival_time;
        services += hbd.service_time;
        count++;
      }
      arrivals /= count;
      services /= count;
      if(ProtocolLog.Monitor.Enabled)
        ProtocolLog.Write(ProtocolLog.Monitor, String.Format(
          "HeartBeat arrivals/services/lost = {0}/{1}/{2}", arrivals, services,
          _lost));
      if((services > arrivals && services > 5) || services > 15)
        _monitor.Sleep();
      lock(_sync) {
        _lost = 0;
      }
    }

    public void WakeUpCallBack(object o, EventArgs args) {
      lock(_sync) {
        _last_arrival = DateTime.UtcNow;
        _hbds = null;
      }
    }
  }

  /**
   * A small class to assist in monitoring the _packet_queue used to announce
   * incoming packets.
   */

  public class PacketQueueMonitor {
    protected class PacketData {
      public float arrival_time, service_time;

      public PacketData(TemporaryPacketData tpd) {
        this.arrival_time = tpd.arrival_time;
        this.service_time = (float) (DateTime.UtcNow - tpd.arrival).TotalSeconds;
      }
    }

    protected class TemporaryPacketData {
      public float arrival_time;
      public DateTime arrival;
      public MemBlock data;

      public TemporaryPacketData(DateTime last_arrival, DateTime now, MemBlock data) {
        this.arrival_time = (float) (now - last_arrival).TotalSeconds;
        this.arrival = now;
        this.data = data;
      }
    }

    protected class PacketHistory: CacheLinkedList<PacketData> {
      public PacketHistory(PacketHistory ph, PacketData pd) : base(ph, pd){}
      public static new int MAX_COUNT = 256;
      public static PacketHistory operator + (PacketHistory ph, PacketData pd) {
        return new PacketHistory(ph, pd);
      }
    }

    protected PacketHistory _ph = null;
    protected DateTime last_arrival;
    protected List<TemporaryPacketData> packets = new List<TemporaryPacketData>();
    protected object _sync = new object();
    protected GlobalMonitor _monitor;

    public PacketQueueMonitor(GlobalMonitor monitor) {
      _monitor = monitor;
      _monitor.WakeUpEvent += this.WakeUpCallBack;
      last_arrival = DateTime.UtcNow;
    }

    public void Add(MemBlock packet) {
      lock(_sync) {
        DateTime now = DateTime.UtcNow;
        TemporaryPacketData tpd = new TemporaryPacketData(last_arrival, now, packet);
        last_arrival = now;
        packets.Add(tpd);
      }
    }

    public void Remove(MemBlock packet) {
      lock(_sync) {
        TemporaryPacketData tpd = CheckForPacket(packet);
        if(tpd == null)
          return;
        PacketData pd = new PacketData(tpd);
        _ph += pd;
      }
    }

    protected TemporaryPacketData CheckForPacket(MemBlock packet) {
      int pos = -1;
      foreach(TemporaryPacketData tpd in packets) {
        pos++;
        if(tpd.data.Equals(packet))
          break;
      }
      TemporaryPacketData res = null;
      if(pos >= 0) {
        res = packets[pos];
        packets.RemoveRange(0, pos);
      }
      return res;
    }

    public void CheckSystem() {
      if(_ph == null)
        return;
      float arrivals = 0, services = 0;
      int count = 0;
      foreach(PacketData pd in _ph) {
        arrivals += pd.arrival_time;
        services += pd.service_time;
        count++;
      }
      arrivals /= count;
      services /= count;
      if(ProtocolLog.Monitor.Enabled)
        ProtocolLog.Write(ProtocolLog.Monitor, String.Format(
          "PacketQueue arrivals/services = {0}/{1}", arrivals, services));
      if((services > arrivals && services > 15) || services > 30)
        _monitor.Sleep();
    }

    public void WakeUpCallBack(object o, EventArgs args) {
      lock(_sync) {
        packets.Clear();
        last_arrival = DateTime.UtcNow;
        _ph = null;
      }
    }
  }
}
