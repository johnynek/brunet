/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

//#define POB_DEBUG

using System;
using System.Threading;
using System.Collections;

using Brunet.Connections;
using Brunet.Transport;
using Brunet.Util;

using Brunet.Messaging;
namespace Brunet.Symphony {

  /**
   * This is an attempt to write a simple version of
   * StructuredShortcutConnectionOverlord which is currently quite complex,
   * difficult to understand, and difficult to debug.
   */
  public class StructuredShortcutConnectionOverlord : ConnectionOverlord {
    
    public StructuredShortcutConnectionOverlord(Node n)
    {
      _sync = new Object();
      lock( _sync ) {
        _node = n;
#if BRUNET_SIMULATOR
        _rand = Node.SimulatorRandom;
#else
        _rand = new Random();
#endif
        _last_connection_time = DateTime.UtcNow;
      /**
       * Every heartbeat we assess the trimming situation.
       * If we have excess edges and it has been more than
       * _trim_wait_time heartbeats then we trim.
       */
        _last_retry_time = DateTime.UtcNow;
        _current_retry_interval = _DEFAULT_RETRY_INTERVAL;

        /**
         * Information related to the target selector feature.
         * Includes statistics such as trim rate and connection lifetimes.
         */
        
        _target_selector = new DefaultTargetSelector();
        _last_optimize_time = DateTime.UtcNow;
        _sum_con_lifetime = 0.0;
        _start_time = DateTime.UtcNow;
        _trim_count = 0;
        _shortcuts = 0;

        /*
         * Register event handlers after everything else is set
         */
        //Listen for connection events:
        _node.ConnectionTable.DisconnectionEvent += DisconnectHandler;
        _node.ConnectionTable.ConnectionEvent += ConnectHandler;
        
        _node.HeartBeatEvent += CheckState;
        _node.HeartBeatEvent += CheckConnectionOptimality;
      }
    }

    ///////  Attributes /////////////////

    protected readonly Random _rand;

    protected int _active;
    protected int _shortcuts;
    //We use this to make sure we don't trim connections
    //too fast.  We want to only trim in the "steady state"
    protected DateTime _last_connection_time;
    protected object _sync;

    protected TimeSpan _current_retry_interval;
    protected DateTime _last_retry_time;

    /** Checks logging is enabled. */
    protected int _log_enabled = -1;
    protected bool LogEnabled {
      get {
        lock(_sync) {
          if (_log_enabled == -1) {
            _log_enabled = ProtocolLog.SCO.Enabled ? 1 : 0;
          }
          return (_log_enabled == 1);
        }
      }
    }
    
    //When we last tried to optimize shortcut.
    protected DateTime _last_optimize_time;
    public static readonly int OPTIMIZE_DELAY = 300;//300 seconds

    //keep some statistics, this will help understand the rate at which coordinates change
    protected double _sum_con_lifetime;
    public double MeanConLifetime {
      get {
        lock(_sync) {
          return _trim_count > 0 ? _sum_con_lifetime/_trim_count : 0.0;
        }
      }
    }

    protected readonly DateTime _start_time;
    protected int _trim_count;
    public double TrimRate {
      get {
        lock(_sync) {
          return ((double) _trim_count)/(DateTime.UtcNow - _start_time).TotalSeconds;
        }
      }      
    }

    //Keeps track of connections that have got a benefit of doubt
    protected Hashtable _doubts_table = new Hashtable();
    protected static readonly int MAX_DOUBT_BENEFITS = 2; 

    //optimizer class for shortcuts.
    protected TargetSelector _target_selector;
    public TargetSelector TargetSelector {
      set {
        lock(_sync) {
          _target_selector = value;
        }
      }
    }

    /*
     * In between connections or disconnections there is no
     * need to recompute whether we need connections.
     * So after each connection or disconnection, this becomes
     * false.
     *
     * These are -1 when we don't know 0 is false, 1 is true
     *
     * This is just an optimization, however, running many nodes
     * on one computer seems to benefit from this optimization
     * (reducing cpu usage and thus the likely of timeouts).
     */
    protected int _need_short;
    protected int _need_bypass = -1;

    /*
     * These are parameters of the Overlord.  These govern
     * the way it reacts and works.
     */

    static public readonly int DESIRED_NEIGHBORS = StructuredNearConnectionOverlord.DESIRED_NEIGHBORS;
    
    ///How many seconds to wait between connections/disconnections to trim
    static protected readonly double TRIM_DELAY = 30.0;
    ///By default, we only wake up every 10 seconds, but we back off exponentially
    static protected readonly TimeSpan _DEFAULT_RETRY_INTERVAL = TimeSpan.FromSeconds(10);
    static protected readonly TimeSpan _MAX_RETRY_INTERVAL = TimeSpan.FromSeconds(60);

    /*
     * We don't want to risk mistyping these strings.
     */
    static protected readonly string STRUC_SHORT = "structured.shortcut";
    /// this is a connection we keep to the physically closest of our logN ring neighbors. 
    static protected readonly string STRUC_BYPASS = "structured.bypass";
    
    /// If we are active, we check to see if we need to make new shortcuts
    override public bool IsActive
    {
      get { return 1 ==_active; }
      set { Interlocked.Exchange(ref _active, value ? 1 : 0); }
    }    

    public int DesiredShortcuts {
      get {
        // No shortcuts for networks smaller than 10 nodes
        int desired_sc = 0;
        if( _node.NetworkSize > 10 ) {
          //0.5*logN
          desired_sc = (int) Math.Ceiling(0.5*Math.Log(_node.NetworkSize)/Math.Log(2.0));
        }
        return desired_sc;
      }
    }
    
    override public bool NeedConnection { get { return NeedShortcut; } }

    /**
     * @returns true if we have too few right shortcut connections
     */
    protected bool NeedShortcut {
      get {
        ConnectionList cl = _node.ConnectionTable.GetConnections(ConnectionType.Structured);
        if(cl.Count < 2 * DESIRED_NEIGHBORS + DesiredShortcuts) {
          //We don't have enough connections for what we need:
          return true;
        }

        lock( _sync ) {
          if( _node.NetworkSize < 10 ) {
            //There is no need to bother with shortcuts on small networks
            return false;
          }

          if( _need_short != -1 ) {
            return (_need_short == 1);
          }

          int shortcuts = 0;
          foreach(Connection c in cl) {
            if(!c.ConType.Equals(STRUC_SHORT)) {
              continue;
            }
            int left_pos = cl.LeftInclusiveCount(_node.Address, c.Address);
            int right_pos = cl.RightInclusiveCount(_node.Address, c.Address);
            
            if( left_pos >= DESIRED_NEIGHBORS &&
                right_pos >= DESIRED_NEIGHBORS ) {
            /*
             * No matter what they say, don't count them
             * as a shortcut if they are one a close neighbor
             */
              shortcuts++;
            }
          }

          if( shortcuts < DesiredShortcuts ) {
            _need_short = 1;
            return true;
          } 
          else {
            _need_short = 0;
            return false;
          }
        }
      }
    }

    protected bool NeedBypass {
      get {
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO, 
                            String.Format("Checking if need bypass"));
        }
        lock(_sync) {
          if (_need_bypass != -1) {
            if (LogEnabled) {
              ProtocolLog.Write(ProtocolLog.SCO, 
                                String.Format("Returning: {0}.", (_need_bypass == 1)));
            }
            return (_need_bypass == 1);
          }

          foreach(Connection c in _node.ConnectionTable.GetConnections(ConnectionType.Structured)) {
            if(!c.ConType.Equals(STRUC_BYPASS)) {
              continue;
            }

            if (LogEnabled) {
              ProtocolLog.Write(ProtocolLog.SCO, String.Format("Returning: false."));
            }
            _need_bypass = 0;
            return false;
          }

          if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.SCO, String.Format("Returning: true."));
          }
          _need_bypass = 1;
          return true;
        }
      }
    }

    public override TAAuthorizer TAAuth { get { return _ta_auth;} }
    protected readonly static TAAuthorizer _ta_auth = new TATypeAuthorizer(
          new TransportAddress.TAType[]{TransportAddress.TAType.Relay,
            TransportAddress.TAType.Subring},
          TAAuthorizer.Decision.Deny,
          TAAuthorizer.Decision.None);
    
    ///////////////// Methods //////////////////////
    
    /**
     * Starts the Overlord if we are active
     *
     * This method is called by the CheckState method
     * IF we have not seen any connections in a while
     * AND we still need some connections
     *
     */
    public override void Activate()
    {
#if POB_DEBUG
      Console.Error.WriteLine("In Activate: {0}", _node.Address);
#endif
      if( IsActive == false ) {
        return;
      }

      DateTime now = DateTime.UtcNow;
      lock( _sync ) {
        if( now - _last_retry_time < _current_retry_interval ) {
          return;
        }

        _last_retry_time = now;
        //Double the length of time we wait (resets to default on connections)
        _current_retry_interval += _current_retry_interval;
        _current_retry_interval = (_MAX_RETRY_INTERVAL < _current_retry_interval) ?
            _MAX_RETRY_INTERVAL : _current_retry_interval;
      }

      if( !_node.IsConnected ) {
        return;
      }

      if( NeedShortcut ) {
      /*
       * If we are trying to get near connections it
       * is not smart to try to get a shortcut.  We
       * need to make sure we are on the proper place in
       * the ring before doing the below:
       */
#if POB_DEBUG
        Console.Error.WriteLine("NeedShortcut: {0}", _node.Address);
#endif
        CreateShortcut();
      } else if (NeedBypass) {
        CreateBypass();
      }
    }

    /**
     * Every heartbeat we take a look to see if we should trim
     *
     * We only trim one at a time.
     */
    protected void CheckState(object node, EventArgs eargs)
    {
      if( IsActive == false ) {
        return;
      }

      TrimConnections();

      if( NeedConnection ) {
        //Wake back up and try to get some
        Activate();
      }
    }

    /**
     * This method is called when a new Connection is added
     * to the ConnectionTable
     */
    protected void ConnectHandler(object contab, EventArgs eargs)
    {
      lock( _sync ) {
        _shortcuts++;
        _last_connection_time = DateTime.UtcNow;
        _current_retry_interval = _DEFAULT_RETRY_INTERVAL;
        _need_short = -1;
        _need_bypass = -1;
      }
    }
    
    /**
     * This method is called when there is a Disconnection from
     * the ConnectionTable
     */
    protected void DisconnectHandler(object connectiontable, EventArgs args)
    { 
      ConnectionEventArgs ceargs = (ConnectionEventArgs)args;
      Connection c = ceargs.Connection;

      lock( _sync ) {
        _shortcuts--;
        _last_connection_time = DateTime.UtcNow;
        _need_short = -1;
        _need_bypass = -1;
        _current_retry_interval = _DEFAULT_RETRY_INTERVAL;
        _doubts_table.Remove(c.Address);
      }

      if( !IsActive ) {
        return;
      }

      if( c.MainType != ConnectionType.Structured ) {
        return;
      }

      if( c.ConType == STRUC_SHORT ) {
        if( NeedShortcut ) {
          CreateShortcut();
        }
      } else if (c.ConType == STRUC_BYPASS) {
        if (NeedBypass) {
          CreateBypass();
        }
      }
    }
    
    /**
     * Initiates shortcut connection creation to a random shortcut target with the
     * correct distance distribution.
     */
    protected void CreateShortcut()
    {
      /*
       * If there are k nodes out of a total possible
       * number of N ( =2^(160) ), the average distance
       * between them is d_ave = N/k.  So we want to select a distance
       * that is at least N/k from us.  We want to do this
       * with prob(dist = d) ~ 1/d.  We can do this by selecting
       * a uniformly distributed p, and sample:
       * 
       * d = d_ave(d_max/d_ave)^p
       *   = d_ave( 2^(p log d_max - p log d_ave) )
       *   = 2^( p log d_max + (1 - p) log d_ave )
       *  
       * since we can go all the way around the ring d_max = N
       * and: log d_ave = log N - log k, but k is the size of the network:
       * 
       * d = 2^( p log N + (1 - p) log N - (1-p) log k)
       *   = 2^( log N - (1-p)log k)
       * 
       */
      double logN = (double)(Address.MemSize * 8);
      double logk = Math.Log( (double)_node.NetworkSize, 2.0 );
      double p = _rand.NextDouble();
      double ex = logN - (1.0 - p)*logk;
      int ex_i = (int)Math.Floor(ex);
      double ex_f = ex - Math.Floor(ex);
      //Make sure 2^(ex_long+1)  will fit in a long:
      int ex_long = ex_i % 63;
      int ex_big = ex_i - ex_long;
      ulong dist_long = (ulong)Math.Pow(2.0, ex_long + ex_f);
      //This is 2^(ex_big):
      BigInteger big_one = 1;
      BigInteger dist_big = big_one << ex_big;
      BigInteger rand_dist = dist_big * dist_long;

      // Add or subtract random distance to the current address
      BigInteger t_add = _node.Address.ToBigInteger();

      // Random number that is 0 or 1
      if( _rand.Next(2) == 0 ) {
        t_add += rand_dist;
      }
      else {
        t_add -= rand_dist;
      }


      byte[] target_int = Address.ConvertToAddressBuffer(new BigInteger(t_add % Address.Full));
      Address.SetClass(target_int, _node.Address.Class);
      Address start = new AHAddress(target_int);

      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.SCO, 
                          String.Format("SCO local: {0}, Selecting shortcut to create close to start: {1}.", 
                                        _node.Address, start));
      }
      //make a call to the target selector to find the optimal
      _target_selector.ComputeCandidates(start, (int) Math.Ceiling(logk), CreateShortcutCallback, null);
    }
    
    /**
     * Callback function that is invoked when TargetSelector fetches candidate scores in a range.
     * Initiates connection setup. 
     * Node: All connection messages can be tagged with a token string. This token string is currenly being
     * used to keep the following information about a shortcut:
     * 1. The node who initiated the shortcut setup.
     * 2. The random target near which shortcut was chosen.
     * @param start address pointing to the start of range to query.
     * @param score_table list of candidate addresses sorted by score.
     * @param current currently selected optimal (nullable) 
     */
    protected void CreateShortcutCallback(Address start, SortedList score_table, Address current) {
      if (score_table.Count > 0) {
        /**
         * we remember our address and the start of range inside the token.
         * token is the concatenation of 
         * (a) local node address
         * (b) random target for the range queried by target selector
         */
        string token = _node.Address + start.ToString();
        //connect to the min_target
        Address min_target = (Address) score_table.GetByIndex(0);
        ISender send = null;
        if (start.Equals(min_target)) {
          //looks like the target selector simply returned our random address
          if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.SCO, 
                              String.Format("SCO local: {0}, Connecting (shortcut) to min_target: {1} (greedy), random_target: {2}.", 
                                            _node.Address, min_target, start));
          }
          //use a greedy sender
          send = new AHGreedySender(_node, min_target);
        } else {
          if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.SCO, 
                              String.Format("SCO local: {0}, Connecting (shortcut) to min_target: {1} (exact), random_target: {2}.", 
                                  _node.Address, min_target, start));
          }
          //use exact sender
          send = new AHExactSender(_node, min_target);
        }
        ConnectTo(send, min_target, STRUC_SHORT, token);
      }
    }

    /**
     * Initiates creation of a bypass connection.
     * 
     */
    protected void CreateBypass() {
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.SCO, 
                          String.Format("SCO local: {0}, Selecting bypass to create.", 
                                        _node.Address));
      }
      double logk = Math.Log( (double)_node.NetworkSize, 2.0 );
      _target_selector.ComputeCandidates(_node.Address, (int) Math.Ceiling(logk), CreateBypassCallback, null);
    }
    
    protected void CreateBypassCallback(Address start, SortedList score_table, Address current) {
      if (score_table.Count > 0) {
        Address min_target = (Address) score_table.GetByIndex(0);
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO, 
                            String.Format("SCO local: {0}, Connecting (bypass) to min_target: {1}", 
                                          _node.Address, min_target));
        }
        ConnectTo(min_target, STRUC_BYPASS);
      }
    }

    /** 
     * Periodically check if our connections are still optimal. 
     */
    protected void CheckConnectionOptimality(object node, EventArgs eargs) {
      DateTime now = DateTime.UtcNow;
      lock(_sync) {
        if ((now - _last_optimize_time).TotalSeconds < OPTIMIZE_DELAY) {
          return;
        }
        _last_optimize_time = now;
      }

      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.SCO, 
                          String.Format("SCO local: {0}, Selcting a random shortcut to optimize.", 
                                        _node.Address));
      }
      double logk = Math.Log( (double)_node.NetworkSize, 2.0 );  
      
      //Get a random shortcut:
      ArrayList shortcuts = new ArrayList();
      foreach(Connection sc in _node.ConnectionTable.GetConnections(STRUC_SHORT) ) {
        /** 
         * Only if we initiated it, we check if the connection is optimal.
         * First half of the token is initiator address, while the other half 
         * is the start of the range.
         */
        string token = sc.State.PeerLinkMessage.Token;
        if (token == null || token == String.Empty) {
          continue;
        }

        string initiator_addr = token.Substring(0, token.Length/2);
        if (initiator_addr == _node.Address.ToString()) {
          shortcuts.Add(sc);
        }
      }
        

      if (shortcuts.Count > 0) {
        // Pick a random shortcut and check for optimality.
        Connection sc = (Connection)shortcuts[ _rand.Next(shortcuts.Count) ];
        string token = sc.State.PeerLinkMessage.Token;
        // Second half of the token is the random target for the shortcut.
        Address random_target = AddressParser.Parse(token.Substring(token.Length/2));
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO, 
                            String.Format("SCO local: {0}, Optimizing shortcut connection: {1}, random_target: {2}.",
                                          _node.Address, sc.Address, random_target));
        }

        _target_selector.ComputeCandidates(random_target, (int) Math.Ceiling(logk), 
                                           CheckShortcutCallback, sc.Address);
      } else {
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO,
                            String.Format("SCO local: {0}, Cannot find a shortcut to optimize.", 
                                          _node.Address));
        }
      }

      //also optimize the bypass connections.
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.SCO, 
                          String.Format("SCO local: {0}, Selecting a bypass to optimize.", 
                                        _node.Address));
      }
      _target_selector.ComputeCandidates(_node.Address, (int) Math.Ceiling(logk), CheckBypassCallback, null);
    }
    
    /**
     * Checks if the shortcut connection is still optimal, and trims it if not optimal.
     * @param random_target random target pointing to the start of the range for connection candidates.
     * @param score_table candidate addresses sorted by scores.
     * @param sc_address address of the current connection.
     */
    protected void CheckShortcutCallback(Address random_target, SortedList score_table, Address sc_address) {
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.SCO, 
                          String.Format("SCO local: {0}, Checking shortcut optimality: {1}.", 
                                _node.Address, sc_address));
      }
      
      int max_rank = (int) Math.Ceiling(0.2*score_table.Count);
      if (!IsConnectionOptimal(sc_address, score_table, max_rank)) {
        Address min_target = (Address) score_table.GetByIndex(0);
        //find the connection and trim it.
        Connection to_trim = null;
        foreach(Connection c in _node.ConnectionTable.GetConnections(STRUC_SHORT) ) {
          string token = c.State.PeerLinkMessage.Token;
          if (token == null || token == String.Empty) {
            continue;
          }

          // First half of the token should be the connection initiator
          string initiator_address = token.Substring(0, token.Length/2);
          if (initiator_address == _node.Address.ToString() && c.Address.Equals(sc_address)) {
            to_trim = c;
            break;
          }
        }
        
        if (to_trim != null) {
          if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.SCO, 
                              String.Format("SCO local: {0}, Trimming shortcut : {1}, min_target: {2}.",
                                            _node.Address, to_trim.Address, min_target));
          }
          lock(_sync) {
            double total_secs = (DateTime.UtcNow - to_trim.CreationTime).TotalSeconds;
            _sum_con_lifetime += total_secs;
            _trim_count++;
          }
          to_trim.Close(_node.Rpc, String.Empty);
        }
      } else {
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO,
                            String.Format("SCO local: {0}, Shortcut is optimal: {1}.", 
                                          _node.Address, sc_address));
        }
      }
    }
    
    /**
     * Checks if we have the optimal bypass connection, and trims the ones that are unnecessary.
     * @param start random target pointing to the start of the range for connection candidates.
     * @param score_table candidate addresses sorted by scores.
     * @param bp_address address of the current connection (nullable).
     */
    protected void CheckBypassCallback(Address start, SortedList score_table, Address bp_address) {
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.SCO, 
                          String.Format("SCO local: {0}, Checking bypass optimality.", 
                                        _node.Address));
      }
      
      ArrayList bypass_cons = new ArrayList();
      foreach(Connection c in _node.ConnectionTable.GetConnections(STRUC_BYPASS) ) {
        string token = c.State.PeerLinkMessage.Token;
        if(token == null || token.Equals(_node.Address.ToString())) {
          continue;
        }
        bypass_cons.Add(c);
      }
      
      int max_rank = bypass_cons.Count > 1 ? 0: (int) Math.Ceiling(0.2*score_table.Count);
      foreach (Connection bp in bypass_cons) {
        if (!IsConnectionOptimal(bp.Address, score_table, max_rank)) {
          Address min_target = (Address) score_table.GetByIndex(0);
          if (LogEnabled) {
            ProtocolLog.Write(ProtocolLog.SCO, 
                              String.Format("SCO local: {0}, Trimming bypass : {1}, min_target: {2}.", 
                                            _node.Address, bp.Address, min_target));
          }
          lock(_sync) {
            double total_secs = (DateTime.UtcNow - bp.CreationTime).TotalSeconds;
            _sum_con_lifetime += total_secs;
            _trim_count++;
          }
          bp.Close(_node.Rpc, String.Empty);
        } else if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO,
              String.Format("SCO local: {0}, Bypass is optimal: {1}.",
                _node.Address, bp));
        }
      }
    }

    /**
     * Checks if connection to the current address is optimal. 
     * Scores can vary over time, and there might be "tight" race for the optimal.
     * We may end up in a situation that we are trimming a connection that is not optimal, even 
     * though the penalty for not using the optimal is marginal. The following algorithm
     * checks that the current selection is in the top-percentile and also the penalty for not
     * using the current optimal is marginal. 
     * @param curr_address address of the current connection target. 
     * @param score_table candidate addresses sorted by scores.
     * @param max_rank maximum rank within the score table, beyond which connection 
     *                 is treated suboptimal.
     */
    protected bool IsConnectionOptimal(Address curr_address, SortedList score_table, int max_rank) {
      if (score_table.Count == 0) {
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO, 
                            String.Format("SCO local: {0}, Not sufficient scores available to determine optimality: {1}.", 
                                          _node.Address, curr_address));
        }
        return true;
      }
            
      bool optimal = false; //if shortcut is optimal.
      bool doubtful = false; //if there is doubt on optimality of this connection.
      int curr_rank = score_table.IndexOfValue(curr_address);
      if (curr_rank == -1) {
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO, 
                            String.Format("SCO local: {0}, No score available for current: {1}.", 
                                          _node.Address, curr_address));
        }
        
        //doubtful case
        doubtful = true;
      } else if (curr_rank == 0) {
        //definitely optimal
        optimal = true;
      } else if (curr_rank <= max_rank) {
        //not the minimum, but still in top percentile.
        double penalty = (double) score_table.GetKey(curr_rank)/(double) score_table.GetKey(0);
        if (LogEnabled) {
          ProtocolLog.Write(ProtocolLog.SCO, 
                            String.Format("SCO local: {0}, Penalty for using current: {1} penalty: {2}).", 
                                          _node.Address, curr_address, penalty));
        }

        //we allow for 10 percent penalty for not using the optimal
        if (penalty < 1.1 ) {
          optimal = true;
        }
      } else {
        if (LogEnabled) {        
          ProtocolLog.Write(ProtocolLog.SCO, 
                            String.Format("SCO local: {0}, Current: {1} too poorly ranked: {2}.", 
                                  _node.Address, curr_address, curr_rank));
        }
      }

      /** 
       * If we are doubtful about the current selection, we will continue to treat it
       * optimal for sometime.
       */
      string log = null;
      lock(_sync) {
        if (optimal) {
          //clear the entry
          _doubts_table.Remove(curr_address);
        } 
        else if (doubtful) { //if we have doubt about the selection
          //make sure that we are not being to generous
          if (!_doubts_table.ContainsKey(curr_address)) {
            _doubts_table[curr_address] = 1;
          } 
          int idx = (int) _doubts_table[curr_address];
          if (idx < MAX_DOUBT_BENEFITS) {
            _doubts_table[curr_address] = idx + 1;
            log = String.Format("SCO local: {0}, Giving benfit: {1} of doubt for current: {2}.", 
                                       _node.Address, idx, curr_address);
            optimal = true;
          } else {
            log = String.Format("SCO local: {0}, Reached quota: {1} on doubts for current: {2}.", 
                                _node.Address, idx, curr_address);
          }
        }
        
        //all efforts to make the connection look optimal have failed
        if (!optimal) {
          //clear the entry
          _doubts_table.Remove(curr_address);          
        }
      } //end of lock
      if (LogEnabled) {
        ProtocolLog.Write(ProtocolLog.SCO, log);
      }
      return optimal;
    }

    /// Determine if there are any unuseful STRUC_SHORT that we can trim
    protected void TrimConnections() {
      if(_shortcuts <= 2 * DesiredShortcuts) {
        return;
      }

      lock( _sync ) {
        TimeSpan elapsed = DateTime.UtcNow - _last_connection_time;
        if( elapsed.TotalSeconds < TRIM_DELAY ) {
          return;
        }
      }

      ArrayList trim_candidates = new ArrayList();
      ConnectionTable tab = _node.ConnectionTable;
      ConnectionList structs = tab.GetConnections(ConnectionType.Structured);
      foreach(Connection c in structs) {
        if(!c.ConType.Equals(STRUC_SHORT)) {
          continue;
        }
        int left_pos = structs.LeftInclusiveCount(_node.Address, c.Address);
        int right_pos = structs.RightInclusiveCount(_node.Address, c.Address);
        // Verify that this shortcut is not close
        if( left_pos >= DESIRED_NEIGHBORS && right_pos >= DESIRED_NEIGHBORS ) {
          trim_candidates.Add(c);
        }
      }

      /*
       * The maximum number of shortcuts we allow is log N,
       * but we only want 1.  This gives some flexibility to
       * prevent too much edge churning
       */
      if(trim_candidates.Count <= 2 * DesiredShortcuts) {
        return;
      }

      /**
       * @todo use a better algorithm here, such as Nima's
       * algorithm for biasing towards more distant nodes:
       */
      //Delete a random trim candidate:
      int idx = _rand.Next( trim_candidates.Count );
      Connection to_trim = (Connection)trim_candidates[idx];
#if POB_DEBUG
     Console.Error.WriteLine("Attempt to trim Shortcut: {0}", to_trim);
#endif
      to_trim.Close(_node.Rpc, "SCO, shortcut connection trim" );
    }
  }
}
