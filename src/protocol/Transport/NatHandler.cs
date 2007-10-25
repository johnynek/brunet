/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2006 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

#define DEBUG

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

using System;
using System.Net;
using System.Collections;
using System.Collections.Generic;

namespace Brunet {

/**
 * Sometimes we learn things about the NAT we may be behind.  This
 * class represents the data we learn
 */
public abstract class NatDataPoint {

  protected DateTime _date;
  public DateTime DateTime { get { return _date; } }
  
  protected TransportAddress _local;
  public TransportAddress LocalTA { get { return _local; } }

  protected TransportAddress _p_local;
  public TransportAddress PeerViewOfLocalTA { get { return _p_local; } }
  
  protected TransportAddress _remote;
  public TransportAddress RemoteTA { get { return _remote; } }
  
  protected TransportAddress _old_ta;
  /**
   * When the mapping changes, this is the previous TA
   */
  public TransportAddress PreviousTA { get { return _old_ta; } }

  protected long _edge_no;
  /**
   * So we don't keep a reference to the Edge, thereby potentially never allowing
   * garbage collection, each Edge is assigned a unique number
   * This is a unique mapping for the life of the Edge
   */
  public long EdgeNumber { get { return _edge_no; } }
}

/**
 * When a NewEdge is created this is the point
 */
public class NewEdgePoint : NatDataPoint {
  public NewEdgePoint(DateTime dt, Edge e) {
    _date = dt;
    _edge_no = e.Number;
    _local = e.LocalTA;
    _remote = e.RemoteTA;
  }
}

/**
 * When an Edge closes, we note it
 */
public class EdgeClosePoint : NatDataPoint {
  public EdgeClosePoint(DateTime dt, Edge e) {
    _date = dt;
    _edge_no = e.Number;
    _local = e.LocalTA;
    _remote = e.RemoteTA;
  }
}

/**
 * When the local mapping changes, record it here:
 */
public class LocalMappingChangePoint : NatDataPoint {
  public LocalMappingChangePoint(DateTime dt, Edge e,
                                 TransportAddress new_ta) {
    _date = dt;
    _edge_no = e.Number;
    _local = e.LocalTA;
    _remote = e.RemoteTA;
    _p_local = new_ta;
  }
}

/**
 * When the local mapping changes, record it here:
 */
public class RemoteMappingChangePoint : NatDataPoint {
  public RemoteMappingChangePoint(DateTime dt, Edge e) {
    _date = dt;
    _edge_no = e.Number;
    _local = e.LocalTA;
    _remote = e.RemoteTA;
  }
}

/**
 * The ordered list of all the NatDataPoint objects
 * provides several methods to make selecting subsets easier
 */
public class NatHistory : CacheLinkedList<NatDataPoint> {
  public NatHistory(NatHistory nh, NatDataPoint ndp) : base(nh, ndp){}
  public static NatHistory operator + (NatHistory nh, NatDataPoint ndp) {
    return new NatHistory(nh, ndp);
  }

  public static new int MAX_COUNT = 2048;

  /**
   * Given a data point, return some object which is a function of it.
   * If this function returns null, the output will be skipped
   */
  public delegate object Filter(NatDataPoint p);


  /**
   * Return an IEnumerable of NatDataPoints which is all the points
   * where f is true.
   */
  public IEnumerable FilteredEnumerator(Filter f) {
    return new FilteredNDP(this, f);
  }

  /**
   * Given an IEnumerable of NatDataPoints, you can filter it to create
   * another.
   * Only the non-null returned values from the Filter will be returned
   * in the IEnumerator
   */
  public class FilteredNDP : IEnumerable {
    protected Filter _filter;
    protected IEnumerable _ie;
    public FilteredNDP(IEnumerable ie, Filter f) {
      _ie = ie;
      _filter = f;
    }

    public IEnumerator GetEnumerator() {
      foreach(NatDataPoint ndp in _ie) {
        object o = _filter(ndp);
        if( o != null ) {
          yield return o;
        }
      }
    }
  }

  /**
   * Gets a list of all the IPAddress objects which may
   * represent NATs we are behind
   */
  public IEnumerable PeerViewIPs() {
    Filter f = delegate(NatDataPoint p) {
      TransportAddress ta = p.PeerViewOfLocalTA;
      if( ta != null ) {
        return ((IPTransportAddress) ta).GetIPAddresses()[0];
      }
      return null;
    };
    IEnumerable e = new FilteredNDP( this, f );
    /*
     * Go through all the addresses, but only keep
     * one copy of each, with the most recent address last.
     */
    ArrayList list = new ArrayList();
    foreach(IPAddress a in e) {
      if( list.Contains(a) ) {
        list.Remove(a);
      } 
      list.Add(a);
    }
    return list;
  }

  /**
   * An IEnumerator of all the LocalTAs (our view of them, not peer view)
   */
  public IEnumerable LocalTAs() {
    Hashtable ht = new Hashtable();
    Filter f = delegate(NatDataPoint p) {
      TransportAddress ta = p.LocalTA;
      if( ta != null && (false == ht.Contains(ta)) ) {
        ht[ta] = true;
        return ta;
      }
      return null;
    };
    return new FilteredNDP( this, f );
  }

  /**
   * Give all the NatDataPoints that have a PeerViewOfLocalTA matching the
   * giving IPAddress
   */
  public IEnumerable PointsForIP(IPAddress a) {
    Filter f = delegate(NatDataPoint p) {
      TransportAddress ta = p.PeerViewOfLocalTA;
      if( ta != null ) {
        if( a.Equals( ((IPTransportAddress)ta).GetIPAddresses()[0] ) ) {
          return p;
        }
      }
      return null;
    };
    return new FilteredNDP(this, f);
  }

}


/**
 * All NatHandlers are subclasses of this class.
 */
public abstract class NatHandler {
  
  /**
   * @return true if the handler can handle this kind of NAT
   */
  abstract public bool IsMyType(IEnumerable hist);

  /**
   * @return a list of TAs which should correspond to the local NAT
   */
  virtual public IList TargetTAs(IEnumerable hist) {
    //Put each TA in once, but the most recently used ones should be first:
    ArrayList tas = new ArrayList();
    foreach(NatDataPoint np in hist) {
      TransportAddress ta = np.PeerViewOfLocalTA;
      if( ta != null ) {
        if( !tas.Contains(ta) ) {
          //If we haven't already seen this, put it in
          tas.Add( ta );
        }
      }
    }
    return tas;  
  }

}

/**
 * For a public node, there is never any port translation, so, local
 * ports always match remote ports.
 * We assume here only one port is used.
 */
public class PublicNatHandler : NatHandler {
  override public bool IsMyType(IEnumerable h) {
    int port = 0;
    bool port_is_set = false;
    bool retv = true;
    foreach(NatDataPoint p in h) {
      int this_port = ((IPTransportAddress) p.LocalTA).Port;
      if( port_is_set == false ) {
        port = this_port;
        port_is_set = true;
      }
      else {
        retv = retv && (this_port == port);
      }
      TransportAddress pv = p.PeerViewOfLocalTA;
      if( pv != null ) {
        //Check that everything is okay:
        retv = retv && (((IPTransportAddress) pv).Port == port);
      }
      if( retv == false ) {
        break;
      }
    }
    return retv;
  }
  /*
   * This is easy, just return the most recent non-null PeerViewTA.
   */
  override public IList TargetTAs(IEnumerable hist) {
    ArrayList l = new ArrayList();
    TransportAddress local = null;
    foreach(NatDataPoint p in hist) {
      if( local == null ) {
        //Get the most recent local
        local = p.LocalTA;
      }
      TransportAddress pv = p.PeerViewOfLocalTA;
      if( pv != null ) {
        l.Add(pv);
        break;
      } 
    }
    if( l.Count == 0 && (local != null) ) {
      //We never found one, just use a local one
      l.Add(local);
    }
    return l;
  }
}

/**
 * This is some kind of default handler which is a last resort mode
 * The algorithm here is to just return the full history of reported
 * TAs or localTAs in the order of most recently to least recently used
 */
public class NullNatHandler : NatHandler {

  /**
   * This NatHandler thinks it can handle anything.
   */
  override public bool IsMyType(IEnumerable h) { return true; }
  
}

/**
 * Handles Cone Nats
 */
public class ConeNatHandler : NatHandler {
  /**
   * The cone nat uses exactly one port on each IP address
   */
  override public bool IsMyType(IEnumerable h) {
    /*
     * We go through the list.  At each moment we see how many active
     * ports there are.  A cone nat should be using 1, but due to NAT mapping
     * changes, it may take us a little while to notice a change, which may
     * make it appear that we are using 2 ports (assuming we notice much faster
     * than the changes happen).
     * It is also a neccesary condition that we have connections to at least two
     * IP addresses using the same port (part of the definition of a cone nat).
     */
    bool multiple_remote_ip_same_port = false;
    int max_active_ports = 0;
    Hashtable port_to_remote = new Hashtable();
    //Reverse the list to go from least recent to most recent:
    ArrayList all_points = new ArrayList();
    foreach(NatDataPoint dp in h) {
      all_points.Add( dp );
    }
    all_points.Reverse();
    Hashtable edge_no_to_most_recent_point = new Hashtable();
    bool is_cone = true;
    foreach(NatDataPoint dp in all_points) {
      TransportAddress ta = dp.PeerViewOfLocalTA;
      if( ta == null ) { continue; }
      int port = ((IPTransportAddress)ta).Port;
      NatDataPoint old_dp = (NatDataPoint)edge_no_to_most_recent_point[ dp.EdgeNumber ];
      
      if( dp is NewEdgePoint ) {
        //There is a new mapping:
        ArrayList l = AddRemoteTA( port_to_remote, port, dp.RemoteTA );
	multiple_remote_ip_same_port |= (l.Count > 1);
        max_active_ports = Math.Max(max_active_ports, port_to_remote.Count);
      }
      else if( dp is EdgeClosePoint ) {
        //Remove a mapping, this obviously can't increase the number of active ports,
        //or change the multiple_remote_ip_same_port variable
        RemoveRemoteTA(port_to_remote, port, dp.RemoteTA);
      }
      else if( dp is LocalMappingChangePoint ) {
        //Look at the old port, remove from the old port, and put into the new port.
        if( old_dp != null ) {
          int old_port = ((IPTransportAddress) old_dp.PeerViewOfLocalTA).Port;
          RemoveRemoteTA(port_to_remote, old_port, old_dp.RemoteTA);
        }
        ArrayList l = AddRemoteTA( port_to_remote, port, dp.RemoteTA );
	multiple_remote_ip_same_port |= (l.Count > 1);
        max_active_ports = Math.Max(max_active_ports, port_to_remote.Count);
      }
      else if (dp is RemoteMappingChangePoint ) {
        //Remove the old RemoteTA, and put in the new one:
        if( old_dp != null ) {
          RemoveRemoteTA(port_to_remote, port, old_dp.RemoteTA);
        }
        ArrayList l = AddRemoteTA( port_to_remote, port, dp.RemoteTA );
	multiple_remote_ip_same_port |= (l.Count > 1);
        max_active_ports = Math.Max(max_active_ports, port_to_remote.Count);
      }
      edge_no_to_most_recent_point[ dp.EdgeNumber ] = dp;
      //is_cone = (max_active_ports < 3) && ( multiple_remote_ip_same_port);
      is_cone = (max_active_ports < 3);
      if( ! is_cone ) {
        //We can stop now, we are clearly not in the cone case.
        break;
      }
    }
    return is_cone;
  }
  //Return the set of TAs for the given port
  protected ArrayList AddRemoteTA(Hashtable ht, int port, TransportAddress ta) {
    ArrayList l = (ArrayList)ht[ port ];
    if (l == null) { l = new ArrayList(); }
    int idx = l.IndexOf(ta);
    if( idx < 0 ) {	
      //We don't already know about this
      l.Add( ta );
    }
    ht[port] = l;
    return l;
  }
  //Remote the TA from the set
  protected void RemoveRemoteTA(Hashtable ht, int port, TransportAddress ta) {
    ArrayList l = (ArrayList)ht[ port ];
    l.Remove(ta);
    if( l.Count == 0 ) {
      //Get this out of here.
      ht.Remove(port);
    }
  }
  /**
   * return the list of TAs that should be tried
   */
  override public IList TargetTAs(IEnumerable hist) {
      /*
       * The trick here is, for a cone nat, we should only report
       * the most recently used ip/port pair. 
       * For safety, we return the most recent two
       */
      ArrayList tas = new ArrayList();
      foreach(NatDataPoint p in hist) {
        TransportAddress last_reported = p.PeerViewOfLocalTA;
        if( last_reported != null ) {
          if( tas.Count == 0 || !last_reported.Equals( tas[0] ) ) {
            tas.Add( last_reported );
          }
          if( tas.Count == 2 ) {
            return tas;
          }
        }
      }
      return tas;
  }
}

public class SymmetricNatHandler : NatHandler {

  ///How many std. dev. on each side of the mean to use
  protected static readonly double SAFETY = 2.0;
  protected static readonly double MAX_STD_DEV = 5.0;

  override public bool IsMyType(IEnumerable h) {
    ArrayList l = PredictPorts(h); 
    return ( 0 < l.Count );
  }

  /*
   * Given an IEnumerable of NatDataPoints, return a list of 
   * ports from most likely to least likely to be the
   * next port used by the NAT
   *
   * @return an empty list if this is not our type
   */
  protected ArrayList PredictPorts(IEnumerable ndps) {
    ArrayList all_diffs = new ArrayList();
    //Get an increasing subset of the ports:
    int prev = Int32.MinValue; 
    int most_recent_port = -1;
    uint sum = 0;
    uint sum2 = 0;
    bool got_extra_data = false;
    TransportAddress.TAType t = TransportAddress.TAType.Unknown;
    string host = String.Empty;
    foreach(NatDataPoint ndp in ndps) {
      if( false == (ndp is EdgeClosePoint) ) {
        //Ignore closing events for prediction, they'll screw up the port prediction
        TransportAddress ta = ndp.PeerViewOfLocalTA;
        if( ta != null ) {
          int port = ((IPTransportAddress) ta).Port;
//          Console.Error.WriteLine("port: {0}", port);
          if( !got_extra_data ) {
            t = ta.TransportAddressType;
            host = ((IPTransportAddress) ta).Host;
            most_recent_port = port;
            got_extra_data = true;
          }
          if( prev > port ) {
            uint diff = (uint)(prev - port); //Clearly diff is always non-neg
            all_diffs.Add( diff );
            sum += diff;
            sum2 += diff * diff;
          }
          prev = port;
        }
      }
    }
    /**
     * Now look at the mean and variance of the diffs
     */
    ArrayList prediction = new ArrayList();
    if( all_diffs.Count > 1 ) {
      double n = (double)all_diffs.Count;
      double sd = (double)sum;
      double mean = sd/n;
      double s2 = ((double)sum2) - sd*sd/n;
      s2 = s2/(double)(all_diffs.Count - 1);
      double stddev = Math.Sqrt(s2);
      //Console.Error.WriteLine("stddev: {0}", stddev);
      if ( stddev < MAX_STD_DEV ) {
        try {
          double max_delta = mean + SAFETY * stddev;
          if( max_delta < mean + 0.001 ) {
            //This means the stddev is very small, just go up one above the
            //mean:
            max_delta = mean + 1.001;
          }
          int delta = (int)(mean - SAFETY * stddev);
          while(delta < max_delta) {
            if( delta > 0 ) {
              int pred_port = most_recent_port + delta;
              prediction.Add(TransportAddressFactory.CreateInstance(t, host, pred_port) );
            }
            else {
              //Increment the max by one just to keep a constant width:
              max_delta += 1.001; //Giving a little extra to make sure we get 1
            }
            delta++;
          }
        }
        catch {
         //Just ignore any bad transport addresses.
        }
      }
      else {
        //The standard deviation is too wide to make a meaningful prediction
      }
    }
    return prediction;
  }

  override public IList TargetTAs(IEnumerable hist) {
    return PredictPorts(hist); 
  }

}

/**
 * The standard IPTables NAT in Linux is similar to a symmetric NAT.
 * It will try to avoid translating the port number, but if it can't
 * (due to another node behind the NAT already using that port to contact
 * the same remote IP/port), then it will assign a new port. 
 *
 * So, we should try to use the "default" port first, but if that doesn't
 * work, use port prediction.
 */
public class LinuxNatHandler : SymmetricNatHandler {
  
  /**
   * Check to see that at least some of the remote ports match the local
   * port
   */
  override public bool IsMyType(IEnumerable h) {
    bool retv = false;
    MakeTargets(h, out retv);
    return retv;
  }
  
  protected IList MakeTargets(IEnumerable h, out bool success) {
    bool there_is_a_match = false;
    int matched_port = 0;
    TransportAddress matched_ta = null;
    foreach(NatDataPoint p in h) {
      TransportAddress l = p.LocalTA;
      TransportAddress pv = p.PeerViewOfLocalTA;
      if( l != null && pv != null ) {
        there_is_a_match = (((IPTransportAddress)l).Port == ((IPTransportAddress) pv).Port);
        if( there_is_a_match ) {
          //Move on.
          matched_port = ((IPTransportAddress) l).Port;
          matched_ta = pv;
          break;
        }
      }
    }
    if( there_is_a_match ) {
      //Now we filter to look at only the unmatched ports:
      NatHistory.Filter f = delegate(NatDataPoint p) {
        TransportAddress pv = p.PeerViewOfLocalTA;
        if( (pv != null) && (((IPTransportAddress) pv).Port != matched_port) ) {
          return p;
        }
        return null;
      };
      //This is all the non-matching data points:
      IEnumerable non_matched = new NatHistory.FilteredNDP(h, f);
      ArrayList l = PredictPorts( non_matched );
      //Put in the matched port at the top of the list:
      l.Insert(0, matched_ta);
      success = true;
      return l;
    }
    else {
      success = false;
      return null;
    }
  }

  public override IList TargetTAs(IEnumerable h) {
    bool success = false;
    IList result = MakeTargets(h, out success);
    if( success ) {
      return result;
    }
    else {
      return new ArrayList();
    }
  }

}

/**
 * This is an enumerable object to create the TAs for a given history
 */
public class NatTAs : IEnumerable {

  protected readonly NatHistory _hist;
  protected volatile ArrayList _list_of_remote_ips;
  protected readonly IEnumerable _local_config;
  protected volatile IEnumerable _generated_ta_list;

  /**
   * @param local_config_tas the list of TAs to use as last resort
   * @param NatHistory history information learned from talking to peers (may be null)
   */
  public NatTAs(IEnumerable local_config_tas, NatHistory hist) {
    _hist = hist;
    _local_config = local_config_tas;
  }
  protected void InitRemoteIPs() {
    NatHistory.Filter f = delegate(NatDataPoint p) {
      TransportAddress ta = p.PeerViewOfLocalTA;
      if( ta != null ) {
        return ((IPTransportAddress)ta).GetIPAddresses()[0];
      }
      return null;
    };
    IEnumerable all_ips = _hist.FilteredEnumerator(f);
    Hashtable ht = new Hashtable();
    foreach(IPAddress a in all_ips) {
      if( false == ht.Contains(a) ) {
        IPAddressRecord r = new IPAddressRecord();
        r.IP = a;
        r.Count = 1;
        ht[a] = r;
      }
      else {
        IPAddressRecord r = (IPAddressRecord)ht[a];
        r.Count++;
      }
    }
    
    ArrayList rips = new ArrayList();
    IDictionaryEnumerator de = ht.GetEnumerator();
    while(de.MoveNext()) {
      IPAddressRecord r = (IPAddressRecord)de.Value;
      rips.Add(r);
    }
    //Now we have a list of the most used to least used IPs
    rips.Sort();
    _list_of_remote_ips = rips;
  }

  protected void GenerateTAs() {
    ArrayList gtas = new ArrayList();
    Hashtable ht = new Hashtable();
    if( _hist != null ) {
      /*
       * we go through the list from most likely to least likely:
       */
      if( _list_of_remote_ips == null ) {
        InitRemoteIPs();
      }
      foreach(IPAddressRecord r in _list_of_remote_ips) {
        IEnumerable points = _hist.PointsForIP(r.IP);
        IEnumerator hand_it = NatTAs.AllHandlers();
        bool yielded = false;
        while( hand_it.MoveNext() && (false == yielded) ) {
          NatHandler hand = (NatHandler)hand_it.Current;
          if( hand.IsMyType( points ) ) {
            ProtocolLog.WriteIf(ProtocolLog.NatHandler, String.Format(
              "NatHandler: {0}", hand.GetType()));
            IList tas = hand.TargetTAs( points );
            foreach(TransportAddress ta in tas) {
              if( false == ht.Contains(ta) ) {
                ht[ta] = true;
                gtas.Add(ta);
              }
            }
            //Break out of the while loop, we found the handler.
            yielded = true;
          }
        }
      }
    }
    //Now we should yield the locally configured points:
    foreach(TransportAddress ta in _local_config) {
      if( false == ht.Contains(ta) ) {
        //Don't yield the same address more than once
        gtas.Add(ta);
      }
    }

    _generated_ta_list = gtas; 
    if(ProtocolLog.UdpEdge.Enabled) {
      int i = 0;
      foreach(TransportAddress ta in _generated_ta_list) {
        ProtocolLog.WriteIf(ProtocolLog.SCO, String.Format(
        "LocalTA({0}): {1}",i,ta));
        i++;
      }
    }
  }

  /**
   * This is the main method, this enumerates (in order) the
   * TAs for this history
   */
  public IEnumerator GetEnumerator() {
    if( _generated_ta_list == null ) {
      GenerateTAs();
    }
    return _generated_ta_list.GetEnumerator();
  }

  /**
   * Enumerator that will go through all the NatHandlers in a fixed order
   */
  static protected IEnumerator AllHandlers() {
    yield return new PublicNatHandler();
    yield return new ConeNatHandler();
    yield return new LinuxNatHandler();
    yield return new SymmetricNatHandler();
    yield return new NullNatHandler();
  }

  protected class IPAddressRecord : IComparable {
    public IPAddress IP;
    public int Count;
    /**
     * Sort them from largest count to least count
     */
    public int CompareTo(object o) {
      if( this == o ) { return 0; }
      IPAddressRecord other = (IPAddressRecord)o;
      if( Count > other.Count ) {
        return -1;
      }
      else if( Count < other.Count ) {
        return 1;
      }
      else {
        return 0;
      }
    }
  }

}

#if BRUNET_NUNIT
[TestFixture]
public class NatTest {

  [Test]
  public void TestPortPrediction() {
    Edge e = new FakeEdge( TransportAddressFactory.CreateInstance("brunet.udp://127.0.0.1:80"),
                           TransportAddressFactory.CreateInstance("brunet.udp://127.0.0.1:1080"));
    NatHistory h = null;
    h = h + new NewEdgePoint(DateTime.UtcNow, e);
    h = h + new LocalMappingChangePoint(DateTime.UtcNow, e,
                         TransportAddressFactory.CreateInstance("brunet.udp://128.128.128.128:80"));
    NatHandler nh = new PublicNatHandler();
    Assert.IsTrue( nh.IsMyType(h), "PublicNatHandler");
    IList tas = nh.TargetTAs(h);
    Assert.IsTrue( tas.Contains(
                     TransportAddressFactory.CreateInstance("brunet.udp://128.128.128.128:80")
                   ), "ConeNatHandler.TargetTAs");
    
    nh = new ConeNatHandler();
    Assert.IsTrue( nh.IsMyType(h), "ConeNatHandler");
    tas = nh.TargetTAs(h);
    //foreach(object ta in tas) { Console.Error.WriteLine(ta); }
    Assert.IsTrue( tas.Contains(
                     TransportAddressFactory.CreateInstance("brunet.udp://128.128.128.128:80")
                   ), "ConeNatHandler.TargetTAs");
   /* 
    * Now, let's try Port prediction:
    */
    int local_port = 80;
    int port = local_port;
    h = null;
    while( port < 86 ) {
      e = new FakeEdge( TransportAddressFactory.CreateInstance("brunet.udp://127.0.0.1:"
                                              + local_port.ToString() ),
                           TransportAddressFactory.CreateInstance("brunet.udp://127.0.0.1:1081"));
      h = h + new NewEdgePoint(DateTime.UtcNow, e);
      
      h = h + new LocalMappingChangePoint(DateTime.UtcNow, e,
                         TransportAddressFactory.CreateInstance("brunet.udp://128.128.128.128:"
                           + port.ToString()
                         ));
      port = port + 1;
    }
    nh = new SymmetricNatHandler();
    Assert.IsTrue( nh.IsMyType(h), "SymmetricNatHandler");
    tas = nh.TargetTAs(h);
    //foreach(object ta in tas) { Console.Error.WriteLine(ta); }
    Assert.IsTrue( tas.Contains(
                     TransportAddressFactory.CreateInstance("brunet.udp://128.128.128.128:86")
                   ), "SymmetricNatHandler.TargetTAs");
    nh = new LinuxNatHandler();
    Assert.IsTrue( nh.IsMyType(h), "LinuxNatHandler");
    tas = nh.TargetTAs(h);
    //foreach(object ta in tas) { Console.Error.WriteLine(ta); }
    Assert.IsTrue( tas.Contains(
                     TransportAddressFactory.CreateInstance("brunet.udp://128.128.128.128:86")
                   ), "LinuxNatHandler.TargetTAs");
    Assert.IsTrue( tas.Contains(
                     TransportAddressFactory.CreateInstance("brunet.udp://128.128.128.128:80")
                   ), "LinuxNatHandler.TargetTAs");
  }

}

#endif

}

