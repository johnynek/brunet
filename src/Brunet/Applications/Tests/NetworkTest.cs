/*
Copyright (C) 2010 David Wolinsky <davidiw@ufl.edu>, University of Florida

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

using Brunet.Concurrent;
using Brunet.Messaging;
using Brunet.Symphony;
using Brunet.Util;

using Mono.Data.SqliteClient;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Threading;

namespace Brunet.Applications.Tests {
  /// <summary>Tests networking conditions across the overlay.</summary>
  public class NetworkTest {
    /// <summary>Stores the results for a single node</summary>
    public class ResultState {
      /// <summary>The remote node</summary>
      public readonly Address Target;
      /// <summary>RTTs between the local and remote nodes</summary>
      public TimeSpan[] RTTs = new TimeSpan[3];
      /// <summary>Hops from the remote node to the local node</summary>
      public short[] Hops = new short[3];
      /// <summary>Current attempt</summary>
      public int CurrentAttempt = 0;
      /// <summary>Start time of the current attempt</summary>
      public DateTime StartTime;

      public ResultState(Address target)
      {
        Target = target;
      }
    }

    /// <summary>The outstanding ResultStates</summary>
    protected Dictionary<Address, bool> _outstanding;
    /// <summary>Maps a Node to a ResultState</summary>
    protected Dictionary<Address, ResultState> _results;
    /// <summary>Timer to instigate running of the test</summary>
    protected readonly FuzzyEvent _fe;
    /// <summary>The Node the test is being performed for</summary>
    protected readonly StructuredNode _node;
    /// <summary>The tests is currently active</summary>
    protected int _running;
    /// <summary>The current iteration of the test</summary>
    protected int _current_iter;
    /// <summary>Thread-safety for the _outstanding and _results</summary>
    protected readonly object _sync;
    /// <summary>The string used to connedct to the DB</summary>
    protected readonly string _db_con;

    public NetworkTest(StructuredNode node)
    {
      _node = node;
      _fe = FuzzyTimer.Instance.DoEvery(Run, 30 * 60 * 1000, 500);
      _running = 0;
      _sync = new object();
      _db_con = String.Format("URI=file:{0}.{1}.db,version=3",
          _node.Realm, GetAddress(_node.Address));
      _current_iter = InitializeDatabase();
      Thread.MemoryBarrier();
      _node.StateChangeEvent += Stop;
    }

    /// <summary>Stops the running of the job, node disconnection also calls it.</summary>
    public void Stop()
    {
      _fe.TryCancel();
    }

    /// <summary>Called when the Node changes state, stops the Test if necessary.</summary>
    protected void Stop(Node n, Node.ConnectionState cs)
    {
      if(cs == Node.ConnectionState.Leaving ||
          cs == Node.ConnectionState.Disconnected)
      {
        Stop();
      }
    }

    /// <summary>Begin a test if there isn't already one running</summary>
    protected void Run(DateTime now)
    {
      if(Interlocked.Exchange(ref _running, 1) == 1) {
        return;
      }

      _results = new Dictionary<Address, ResultState>();
      _outstanding = new Dictionary<Address, bool>();
      ResultState rs = new ResultState(_node.Address);
      _results[rs.Target] = rs;
      _outstanding[rs.Target] = true;
      Invoke(rs.Target);
    }

    /// <summary>Starts a test on the specified node.</summary>
    protected void Invoke(Address addr)
    {
      Channel q = new Channel(1);
      q.CloseEvent += delegate(object send, EventArgs ea) {
        RpcResult result = null;
        if(q.Count > 0) {
          result = q.Dequeue() as RpcResult;
        }
        ProcessResults(addr, result);
      };
      AHSender sender = new AHGreedySender(_node, addr);
      _node.Rpc.Invoke(sender, q, "sys:link.GetNeighbors");
    }

    /// <summary>Process a new results and determine whether or not to execute
    /// more tests on this node and the neighbors it returned.</summary>
    protected void ProcessResults(Address addr, RpcResult result)
    {
      ResultState rs = null;
      lock(_sync) {
        rs = _results[addr];
      }

      if(result == null || result.Statistics.SendCount != 1) {
        rs.RTTs[rs.CurrentAttempt] = TimeSpan.MinValue;
        rs.Hops[rs.CurrentAttempt] = -1;
      } else {
        rs.RTTs[rs.CurrentAttempt] = DateTime.UtcNow - rs.StartTime;
        rs.Hops[rs.CurrentAttempt] = (result.ResultSender as AHSender).HopsTaken;
      }

      rs.CurrentAttempt++;

      List<ResultState> to_invoke = new List<ResultState>();
      bool finished = false;

      Hashtable data = null;
      if(result != null) {
        data = result.Result as Hashtable;
      }

      lock(_sync) {
        if(rs.CurrentAttempt == 3) {
          _outstanding.Remove(addr);
        } else {
          to_invoke.Add(rs);
        }
        if(data != null) {
          foreach(string peer in data.Values) {
            Address peer_addr = AddressParser.Parse(peer);
            if(_results.ContainsKey(peer_addr)) {
              continue;
            }
            ResultState nrs = new ResultState(peer_addr);
            to_invoke.Add(nrs);
            _results[peer_addr] = nrs;
            _outstanding[peer_addr] = true;
          }
        }
        if(_outstanding.Count == 0) {
          finished = true;
        }
      }

      if(finished) {
        ThreadPool.QueueUserWorkItem(delegate(object o) { Finished(); } );
      }

      foreach(ResultState nrs in to_invoke) {
        nrs.StartTime = DateTime.UtcNow;
        Invoke(nrs.Target);
      }
    }

    /// <summary>Creates the database if necessary and returns the current
    /// iteration.</summary>
    protected int InitializeDatabase()
    {
      IDbConnection dbcon = new SqliteConnection(_db_con);
      dbcon.Open();
      IDbCommand dbcmd = dbcon.CreateCommand();
      string sql = "CREATE TABLE crawl (iter INTEGER, Address TEXT, Hits INTEGER, " +
        "Rtt REAL, Hops REAL)";
      dbcmd.CommandText = sql;
      try {
        dbcmd.ExecuteNonQuery();
      } catch {
      }

      sql = "CREATE TABLE iterations (iter INTEGER, time INTEGER)";
      dbcmd.CommandText = sql;
      try {
        dbcmd.ExecuteNonQuery();
      } catch {
      }

      // Set _current_iter to how many iterations there exist already
      sql = "SELECT iter FROM iterations";
      dbcmd.CommandText = sql;
      int current_iter = dbcmd.ExecuteNonQuery();
      dbcmd.Dispose();

      dbcon.Close();
      return current_iter;
    }

    /// <summary>The test is finished, write the results to the DB</summary>
    protected void Finished()
    {
      IDbConnection dbcon = new SqliteConnection(_db_con);
      dbcon.Open();
      IDbCommand dbcmd = dbcon.CreateCommand();

      string sql = "INSERT INTO iterations (iter, time) VALUES ( " +
        _current_iter + ", datetime())";
      dbcmd.CommandText = sql;
      dbcmd.ExecuteNonQuery();

      foreach(ResultState rs in _results.Values) {
        int hits = 0;
        double rtt = 0;
        double hops = 0;

        for(int i = 0; i < 3; i++) {
          if(rs.RTTs[i] == TimeSpan.MinValue) {
            continue;
          }
          hits++;
          rtt += rs.RTTs[i].TotalSeconds;
          hops += rs.Hops[i];
        }

        if(hits > 0) {
          rtt /= hits;
          hops /= hits;
        } else {
          rtt = -1;
          hops = -1;
        }

        sql = "INSERT INTO crawl (iter, Address, Hits, Rtt, Hops) " +
          " VALUES ( " + _current_iter + ", \"" + GetAddress(rs.Target) + "\", " +
          hits + ", " + rtt + ", " + hops + ")";
        dbcmd.CommandText = sql;
        dbcmd.ExecuteNonQuery();
      }
      dbcmd.Dispose();
      dbcon.Close();

      _results = null;
      _outstanding = null;
      _current_iter++;
      Interlocked.Exchange(ref _running, 0);
    }

    /// <summary>Returns the unique part of an address</summary>
    public static string GetAddress(Address addr)
    {
      return addr.ToString().Substring(12);
    }
  }
}
