using System;
using System.Collections;

namespace Ipop {
  public struct Lease {
    public byte [] ip;
    public byte [] hwaddr;
    public DateTime expiration;
  }

  class DHCPLease {
    SortedList leaselist;
    int index, end;

    public DHCPLease(int end) {
      leaselist = new SortedList(); 
      index = 0;
      this.end = end;
    }

    public byte [] GetLease(byte [] hwaddr, byte [] ip) {
      int index = CheckForPreviousLease(hwaddr);
      if(keygen(ip) != 0 && index == -1)
        index = CheckRequestedIP(ip);
      if(index == -1)
        index = GetNextAvailableIP(hwaddr);
      if(index >= 0) {
        Lease lease = (Lease) leaselist.GetByIndex(index);
        lease.hwaddr = hwaddr;
        lease.expiration = DateTime.Now.AddDays(7);
        leaselist.SetByIndex(index, lease);
        ip = lease.ip;
      }
      return ip;
    }

    public int CheckForPreviousLease(byte [] hwaddr) {
      int key = keygen(hwaddr);
      if(leaselist.Contains(key))
        return leaselist.IndexOfKey(key);
      return -1;
    }

    public int CheckRequestedIP(byte [] ip) {
      int start = 0, end = leaselist.Count;
      int index = leaselist.Count / 2, ip_check;
      int ip_key = keygen(ip), count = 0, term = (int) Math.Ceiling(Math.Log((double) end));

      if(leaselist.Count == 0)
        return -1;

      while(count != term) {
        ip_check = keygen(((Lease)leaselist.GetByIndex(index)).ip);
        if(ip_key == ip_check)
          return index;
        else if(ip_key > ip_check) {
          start = index;
          index = (index + end) / 2;
        }
        else {
          end = index;
          index = (start + index) / 2;
        }
        count++;
      }
      return -1;
    }

    public int GetNextAvailableIP(byte [] hwaddr) {
      int temp = this.index, count = leaselist.Count;
      DateTime now = DateTime.Now;

      /* Unused Lease, create new Lease and return its index */
      if(this.index == count) {
        Lease lease = new Lease();
        if(count == 0)
          lease.ip = new byte[] {10, 128, 0, 2};
        else
          lease.ip = IncrementIP(((Lease) leaselist.GetByIndex(this.index - 1)).ip);
        leaselist.Add(keygen(hwaddr), lease);
        return this.index++;
      }

      /* Find the first expired lease and return it */
      do {
        if(this.index == this.end)
          this.index = 0;
        if(((Lease)leaselist[this.index]).expiration < now)
          return this.index;
        this.index++;
      } while(this.index != temp);
      return -1;
    }

    public int keygen(byte [] input) {
      int key = 0;
      for(int i = 0; i < input.Length; i++)
        key += input[i] << 8*i;
      return key;
    }

    public byte [] IncrementIP(byte [] ip) {
      if(ip[3] == 0 || ip[3] == 1) {
        ip[3] = 2;
      }
      else if(ip[3] == 255) {
        ip[3] = 2;
        if(ip[2] < 255)
          ip[2]++;
        else {
          ip[2] = 0;
          if(ip[1] < 255)
            ip[1]++;
          else 
            ip[1] = 128;
        }
      }
      else {
        ip[3]++;
      }

      return ip;
    }
  }
}