/**
   The class implements the Brunet routing table for IP packets.
**/
using System;
using Brunet;
using System.Collections;
using System.Net;

namespace Ipop {
  class RoutingTableEntry {
    //an entry times out after these many seconds 
    //readonly static int TIMEOUT = 
    private IPAddress addr;
    private Address brunetID;
    private int timeout;
    public IPAddress IPAddr 
    {
      get {
	return addr;
      }
    }
    public Address BrunetID 
    {
      get {
	return brunetID;
      }
      set {
	brunetID = value;
      }
    }
    public RoutingTableEntry(IPAddress a, Address b) {
      addr = a;
      brunetID = b;
    }
  }
  
  public class RoutingTable {
    private static int ROUTING_TABLE_SIZE = 100;
    private RoutingTableEntry[] routes;
    //default constructor
    public RoutingTable() {
      routes = new RoutingTableEntry[ROUTING_TABLE_SIZE];
    }
    //method to add a route entry
    public bool AddRoute(IPAddress addr, Address brunetID) {
      int emptySlot = -1;
      for (int i = 0; i < ROUTING_TABLE_SIZE; i++) {
	if (routes[i] == null) {
	  emptySlot = i;
	  continue;
	}
	if (routes[i].IPAddr.Equals(addr)) {
	  //reuse the slot
	  Console.WriteLine("reusing a slot");
	  routes[i].BrunetID = brunetID;
	  return true;
	}
      }
      //finally insert; if empty slot available
      if (emptySlot == -1) {
	return false;
      }
      routes[emptySlot] = new RoutingTableEntry(addr, brunetID);
      return true;
    }

    public Address SearchRoute(IPAddress addr) {
      for (int i = 0; i < ROUTING_TABLE_SIZE; i++) {
	 if (routes[i] == null) {
	continue;
	  }
      if (routes[i].IPAddr.Equals(addr)) {
	  return routes[i].BrunetID;
	}
       }
       return null;
    }
    public bool DeleteRoute(IPAddress addr) {
      for (int i = 0; i < ROUTING_TABLE_SIZE; i++) {
	if (routes[i] == null) {
	  continue;
	}
	if (routes[i].IPAddr.Equals(addr)) {
	  routes[i] = null;
	  return true;
	}
      }
      return false;
    }
  }
}
