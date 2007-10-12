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

    protected PacketHistory ph = null;
    protected DateTime last_arrival;
    protected List<TemporaryPacketData> packets = new List<TemporaryPacketData>();
    protected object _sync = new object();
    protected Node _node;
    protected static int sleep_time = 60 * 1000;
    protected static int MAXIMUM_SLEEP_TIME = 30 * 60 * 60 * 1000;
    protected static int MINIMUM_SLEEP_TIME = 60 * 1000;

    public PacketQueueMonitor(Node node) {
      _node = node;
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
        ph += pd;
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
      if(ph == null)
        return;
      float arrivals = 0, services = 0;
      int count = 0;
      foreach(PacketData pd in ph) {
        arrivals += pd.arrival_time;
        services += pd.service_time;
        count++;
      }
      arrivals /= count;
      services /= count;
      ProtocolLog.WriteIf(ProtocolLog.Monitor, String.Format(
        "arrivals/services = " + arrivals + "/" + services));
      if((services > arrivals && services > 15) || services > 30) {
        ProtocolLog.WriteIf(ProtocolLog.Monitor, "Sleeping for " 
          + sleep_time / 1000 + " seconds");
        _node.sleep_mode = true;
        System.Threading.Thread.Sleep(sleep_time);
        lock(_sync) {
          packets.Clear();
          last_arrival = DateTime.UtcNow;
          ph = null;
        }
        _node.sleep_mode = false;

        ProtocolLog.WriteIf(ProtocolLog.Monitor, "Sleeping over");
        if(sleep_time < MAXIMUM_SLEEP_TIME)
          sleep_time *= 2;
      }
      else if(sleep_time > MINIMUM_SLEEP_TIME)
        sleep_time /= 2;
    }
  }
}
