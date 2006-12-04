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

using System;
using System.Net;
using System.Collections;

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

  protected int _edge_no;
  /**
   * So we don't keep a reference to the Edge, thereby potentially never allowing
   * garbage collection, each Edge is assigned a unique number
   * This is a unique mapping for the life of the Edge
   */
  public int EdgeNumber { get { return _edge_no; } }

  static protected WeakHashtable _edge_nos;
  static int _next_edge_no;
  static NatDataPoint() {
    _edge_nos = new WeakHashtable();
    _next_edge_no = 1;
  }

  /**
   * Return the edge number for the given Edge.  If we don't
   * have a number for it, return 0
   */
  static public int GetEdgeNumberOf(Edge e) {
    int no = 0;
    object v = _edge_nos[e];
    if( v != null ) {
      no = (int)v;
    }
    return no;
  }

  protected void SetEdgeNumber(Edge e) {
    if( e == null ) {
      _edge_no = 0;
    }
    else {
      object v = _edge_nos[e];
      if( v != null ) {
        _edge_no = (int)v;
      }
      else {
        _edge_no = _next_edge_no;
        _next_edge_no++;
        _edge_nos[e] = _edge_no;
      }
    }
  }
}

/**
 * When you start an EdgeListener you often know (or guess) what the
 * TransportAddress to connect to that listern is.  This represents
 * that kind of data point
 */
public class LocalConfigPoint : NatDataPoint {
  public LocalConfigPoint(DateTime dt, TransportAddress ta) {
    SetEdgeNumber(null);
    _local = ta;
    _date = dt;
  }
}

/**
 * When a NewEdge is created this is the point
 */
public class NewEdgePoint : NatDataPoint {
  public NewEdgePoint(DateTime dt, Edge e) {
    _date = dt;
    SetEdgeNumber(e);
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
    SetEdgeNumber(e);
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
    SetEdgeNumber(e);
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
    SetEdgeNumber(e);
    _local = e.LocalTA;
    _remote = e.RemoteTA;
  }
}

/**
 * The ordered list of all the NatDataPoint objects
 * provides several methods to make selecting subsets easier
 */
public class NatHistory {

  protected ArrayList _points;

  public delegate bool Filter(NatDataPoint p);

  public NatHistory() {
    _points = new ArrayList();
  }
  public NatHistory(NatDataPoint p) {
    _points = new ArrayList();
    _points.Add(p);
  }
  
  protected NatHistory(ArrayList l) {
    _points = l;
  }

  public IEnumerator GetEnumerator() {
    return _points.GetEnumerator();
  }

  /**
   * Return an IEnumerable of NatDataPoints which is all the points
   * where f is true.
   */
  public IEnumerable FilteredEnumerator(Filter f) {
    return new FilteredNDP( _points, f);
  }

  /**
   * Given an IEnumerable of NatDataPoints, you can filter it to create
   * another.
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
        if( _filter(ndp) ) {
          yield return ndp;
        }
      }
    }
  }

  /**
   * Allows us to enumerate over all the distinct IPAddress objects which
   * are reported to us by our peers
   */
  protected class IPEnumerator : IEnumerable {
    protected IEnumerable _points;
    public IPEnumerator(IEnumerable p) {
      _points = p;
    }

    public IEnumerator GetEnumerator() {
     Hashtable ht = new Hashtable();
     foreach(NatDataPoint p in _points) {
      TransportAddress ta = p.PeerViewOfLocalTA;
      if( ta != null ) {
        IPAddress this_ip = (IPAddress)(ta.GetIPAddresses()[0]);
        if ( ht.Contains( this_ip ) == false ) {
          ht[this_ip] = true;
          yield return this_ip;
        }
      }
     }
    }
  }
  /**
   * Gets a list of all the IPAddress objects which may
   * represent NATs we are behind
   */
  public IEnumerable PeerViewIPs() {
    return new IPEnumerator( _points );
  }

  /**
   * An IEnumerator of all the LocalTAs (our view of them, not peer view)
   */
  public IEnumerator LocalTAs() {
    Hashtable ht = new Hashtable();
    foreach(NatDataPoint p in _points) {
      TransportAddress ta = p.LocalTA;
      if( ta != null && (false == ht.Contains(ta)) ) {
        ht[ta] = true;
        yield return ta;
      }
    }
  }

  /**
   * Give all the NatDataPoints that have a PeerViewOfLocalTA matching the
   * giving IPAddress
   */
  public IEnumerable PointsForIP(IPAddress a) {
    Filter f = delegate(NatDataPoint p) {
      IPAddress this_ip = (IPAddress)(p.PeerViewOfLocalTA.GetIPAddresses()[0]);
      return a.Equals(this_ip);
    };
    return new FilteredNDP(_points, f);
  }

  /**
   * Makes a new history and returns it
   */
  public NatHistory Add(NatDataPoint p) {
    ArrayList np = (ArrayList)_points.Clone();
    np.Add(p);
    return new NatHistory(np);
  }

}


/**
 * All NatHandlers are subclasses of this class.
 */
public abstract class NatHandler {
  
  protected NatHistory _hist;
  public NatHistory History {
    get { return _hist; }
    set { _hist = value; }
  }

  /**
   * @return true if the handler can handle this kind of NAT
   */
  abstract public bool IsMyType(NatHistory hist);

  abstract public IList TargetTAs { get; }

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
  override public bool IsMyType(NatHistory h) { return true; }
  
  /**
   * return the list of TAs that should be tried
   */
  override public IList TargetTAs {
    get {
      Hashtable ht = new Hashtable();
      //Put each TA in once, but the most recently used ones should be first:
      ArrayList tas = new ArrayList();
      foreach(NatDataPoint np in _hist) {
        TransportAddress ta = np.PeerViewOfLocalTA;
        if( ta == null ) {
          //We use our own guess:
          ta = np.LocalTA;
        }
        if( ht.Contains(ta) == false ) {
          tas.Add( ta );
        }
      }
      //Now reverse it so the most recently used is first:
      tas.Reverse();
      return tas;
    }
  }
}

/**
 * Handles Cone Nats
 */
public class ConeNatHandler : NatHandler {
  /**
   * The cone nat uses exactly one port on each IP address
   * @todo handle NAT mapping changes which can occasionally occur on a Cone NAT
   */
  override public bool IsMyType(NatHistory h) {
    foreach( IPAddress a in h.PeerViewIPs() ) {
      bool got_first = false;
      int port = 0;
      foreach( NatDataPoint dp in h.PointsForIP(a) ) {
        if( !got_first ) {
          port = dp.PeerViewOfLocalTA.Port;
          got_first = true;
        }
        else {
          if( port != dp.PeerViewOfLocalTA.Port ) {
            //There are several ports on the IP mapping to our address
            return false;
          }
        }

      }
    }
    return true;
  }

  /**
   * return the list of TAs that should be tried
   */
  override public IList TargetTAs {
    get {
      //When we add LocalTAs, we want to make sure they aren't duplicated
      Hashtable ht = new Hashtable();
      //Put each IP in once, but the most recently used ones should be first:
      ArrayList tas = new ArrayList();
      foreach(IPAddress a in _hist.PeerViewIPs() ) {
        TransportAddress most_recent = null;
        //Loop through, but only keep the last TA
        foreach(NatDataPoint dp in _hist.PointsForIP(a) ) {
          most_recent = dp.PeerViewOfLocalTA;
        }
        if( most_recent != null ) {
          tas.Add( most_recent );
          ht[ most_recent ] = true;
        }
      }
      //Reverse the list to make most recent first:
      tas.Reverse();
      //Now put in the localTAs:
      ArrayList locals = new ArrayList();
      IEnumerator ie = _hist.LocalTAs();
      while( ie.MoveNext() ) {
        TransportAddress ta = (TransportAddress)ie.Current;
        if( false == ht.Contains(ta) ) {
          //This is a new one:
          locals.Add(ta);
        }
      }
      locals.Reverse();
      tas.AddRange( locals );
      return tas;
    }
  }
}

/**
 * This is the object that creates the correct NatHandler for a given NatHistory
 */
public class NatHandlerFactory {

  static public NatHandler CreateHandler(NatHistory hist) {
    /*
     * we go through the list from most likely to least likely:
     */
    IEnumerator hand_it = NatHandlerFactory.AllHandlers();
    while( hand_it.MoveNext() ) {
      NatHandler hand = (NatHandler)hand_it.Current;
      if( hand.IsMyType( hist ) ) {
        hand.History = hist;
        return hand;
      }
    }
    //No one can handle this....
    return null;
  }

  /**
   * Enumerator that will go through all the NatHandlers in a fixed order
   */
  static public IEnumerator AllHandlers() {
    yield return new ConeNatHandler();
    yield return new NullNatHandler();
  }

}


}

