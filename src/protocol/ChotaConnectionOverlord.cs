/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005-2006  University of Florida

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

namespace Brunet {
  public class NodeRankComparer : System.Collections.IComparer {
    public int Compare(object x, object y) {
      if( x == y ) {
        //This is trivial, but we need to deal with it:
        return 0;
      }
      NodeRankInformation x1 = (NodeRankInformation) x;
      NodeRankInformation y1 = (NodeRankInformation) y;
      if (x1.Equals(y1) && x1.Count == y1.Count) {
        /*
         * Since each Address is in our list at most once,
         * this is an Error, so lets print it out and hope
         * someone sees it.
         */
	      return 0;
      } else if (x1.Count <= y1.Count) {
	      return 1;
      } else if (x1.Count > y1.Count) {
	      return -1;
      }
      return -1;
    }
  }

  public class NodeRankInformation { 
    //address of the node
    private Address _addr;
    //rank - score is a better name though
    private int _count;
    
    //constructor
    public NodeRankInformation(Address addr) {
      _addr = addr;
      _count = 0;
    }
    public int Count {
      get { return _count; }
      set { _count = value; }
    }

    public Address Addr { get { return _addr; } }

    override public bool Equals(Object other ) {
      if( Object.ReferenceEquals(other, this) ) {
        return true;
      }

      NodeRankInformation other1 =  other as NodeRankInformation;
      if( Object.ReferenceEquals(other1, null)) {
        return false;
      } else if (_addr.Equals(other1.Addr)) {
	      return true;
      }
      return false;
    }

    override public int GetHashCode() {
      // This should be safe, we shouldn't have more than one per Address.
      return _addr.GetHashCode();
    }

    override public string ToString() {
      return _addr.ToString() + ":" + _count;
    }
  }

  /** The following is what we call a ChotaConnectionOverlord.
   *  This provides high-performance routing by setting up direct
   *  structured connections between pairs of highly communicating nodes.
   *  Chota - in Hindi means small. 
   */
  public class ChotaConnectionOverlord : ConnectionOverlord, IDataHandler {
    //used for locking
    protected object _sync;
    //our random number generator
    protected Random _rand;

    //if the overlord is active
    protected bool _active;
    
    //minimum score before we start forming chota connections
    private static readonly int MIN_SCORE_THRESHOLD = 5;

    //the maximum number of Chota connections we plan to support
    private static readonly int max_chota = 200;
    
    //maximum number of entries in the node_rank table
    private static readonly int node_rank_capacity = max_chota;

    //hashtable of destinations. for each destination we maintain 
    //how frequently we communicate with it. Just like the LRU in virtual
    // memory context - Arijit Ganguly. 
    protected ArrayList node_rank_list;
    /*
     * Allows us to quickly look up the node rank for a destination
     */
    protected Hashtable _dest_to_node_rank;

    //node rank comparer
    protected NodeRankComparer _cmp;

    protected const int _sample_size = 4;
    
    /*
     * We don't want to risk mistyping these strings.
     */
    static protected readonly string struc_chota = "structured.chota";

    public ChotaConnectionOverlord(Node n)
    {
      _node = n;
      _cmp = new NodeRankComparer();
      _sync = new object();
      _rand = new Random();
      node_rank_list = new ArrayList();
      _dest_to_node_rank = new Hashtable();

      lock( _sync ) {
      	// we assess trimming/growing situation on every heart beat
        _node.HeartBeatEvent += this.CheckState;
	      //subscribe the ip_handler to IP packets
        _node.GetTypeSource(PType.Protocol.IP).Subscribe(this, null);
      }
    }

    /**
     * On every activation, the ChotaConnectionOverlord trims any connections
     * that are unused, and also creates any new connections of needed
     * 
     */
    override public void Activate() {
      if (!IsActive) {
	      return;
      }

      IEnumerable chota_cons = _node.ConnectionTable.GetConnections(struc_chota);

      // Trim and add OUTSIDE of the lock!
      List<Edge> to_trim = new List<Edge>();
      List<Address> to_add = new List<Address>();

    	lock(_sync) { //lock the score table
        SortTable();
        // Find the guys to trim....
        for (int i = node_rank_list.Count - 1; i >= max_chota && i > 0; i--) {
          NodeRankInformation node_rank = (NodeRankInformation) node_rank_list[i];
          foreach(Connection c in chota_cons) {
            if(node_rank.Addr.Equals(c.Address)) {
              to_trim.Add(c.Edge);
            }
          }
        }

        // Find guys to connect to!
        for (int i = 0; i < node_rank_list.Count && i < max_chota; i++) {
          //we are traversing the list in descending order of 
          NodeRankInformation node_rank = (NodeRankInformation) node_rank_list[i];
          if (node_rank.Count < MIN_SCORE_THRESHOLD ) {
            //too low score to create a connection
            continue;
          }
          to_add.Add(node_rank.Addr);
        }
      }

      foreach(Edge e in to_trim) {
        _node.GracefullyClose(e, "From Chota, low score trim.");
      }

      foreach(Address addr in to_add) {
	      ConnectTo(addr, struc_chota);
      }
    }
    
    override public bool NeedConnection { get { return true; } }

    public override bool IsActive 
    {
      get { return _active; }
      set { _active = value; }
    }

    /**
     * We count incoming IP packets here
     */
    public void HandleData(MemBlock p, ISender from, object state) {
      AHSender ahs = from as AHSender;
      if( ahs != null ) {
        Address dest = ahs.Destination;
        // Sample every 1 / _sample_size
        if( _rand.Next(_sample_size) != 0 ) {
          return;
        }

        lock(_sync) {
          NodeRankInformation node_rank =
            (NodeRankInformation) _dest_to_node_rank[dest];
          if( node_rank == null ) {
            node_rank = new NodeRankInformation(dest);
            node_rank_list.Add( node_rank );
            _dest_to_node_rank[dest] = node_rank;
          }
          // Increment by _sample_size
          node_rank.Count += _sample_size;
        }
      }
    }

    /**
     * We only need to do this before we take action based on the
     * table, in the mean time, it can get disordered
     */
    protected void SortTable() {
      lock( _sync ) {
        //Keep the table sorted according to _cmp
        node_rank_list.Sort( _cmp );
	      if (node_rank_list.Count > node_rank_capacity) {
          //we are exceeding capacity
          //trim the list
          int rmv_idx = node_rank_list.Count - 1;
          NodeRankInformation nr = (NodeRankInformation)node_rank_list[ rmv_idx ];
	        node_rank_list.RemoveAt(rmv_idx);    
          _dest_to_node_rank.Remove( nr.Addr );
	      }
      }
    }

    /**
     * On every heartbeat this method is invoked.
     * Sort the table, decrement node rank and run Activate.
     */ 
    public void CheckState(object node, EventArgs eargs) {
      if( _rand.Next(_sample_size) != 0 ) {
        return;
      }

      lock(_sync) {
        SortTable();
        foreach(NodeRankInformation node_rank in node_rank_list) {
          if (node_rank.Count > 0) {
            node_rank.Count -= _sample_size;
          }
        }
      }

      Activate();
    }
  }
}
