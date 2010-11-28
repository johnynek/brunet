using Mono.Data.Sqlite;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

using Brunet.Simulator.Transport;
using Brunet.Util;

namespace Brunet.Simulator.Regression {
  public class SystemTest : RegressionTest {
    protected class Result {
      public readonly long Memory;
      public readonly long Time;
      public readonly long Throughput;
      
      public Result(long memory, long time, long throughput)
      {
        Memory = memory;
        Time = time;
        Throughput = throughput;
      }
    }

    protected delegate Result Test();
    readonly protected Test[] _tests;

    public SystemTest(RegressionParameters p) : base(p)
    {
      _tests = new Test[] { Normal };
    }

    override protected void RunTests()
    {
      foreach(Test test in _tests) {
        StoreTestResult(test, test());
      }
    }

    override protected void SetupResultTable()
    {
      IDbConnection dbcon = new SqliteConnection(_db_con);
      dbcon.Open();
      IDbCommand dbcmd = dbcon.CreateCommand();

      string sql = "CREATE TABLE results (iter INTEGER, testid INTEGER, time INTEGER, memory INTEGER, throughput INTEGER)";
      dbcmd.CommandText = sql;
      try {
        dbcmd.ExecuteNonQuery();
      } catch {
      }

      dbcmd.Dispose();
      dbcon.Close();
    }

    /// <summary>This method stores the test results to the database.</summary>
    protected void StoreTestResult(Test test, Result result)
    {
      IDbConnection dbcon = new SqliteConnection(_db_con);
      dbcon.Open();
      IDbCommand dbcmd = dbcon.CreateCommand();

      string method = test.Method.Name;
      int testid = GetTestId(dbcmd, method);
      string sql = "INSERT INTO results (iter, testid, memory, time, throughput) VALUES (" +
        CurrentIter + ", " + testid + ", " + result.Memory + ", " + result.Time + ", " +
        result.Throughput + ")";

      dbcmd.CommandText = sql;
      dbcmd.ExecuteNonQuery();

      dbcmd.Dispose();
      dbcon.Close();
    }

    protected Result Normal()
    {
      var rstart = System.DateTime.UtcNow;
      var vstart = DateTime.UtcNow;
      Simulator sim = new Simulator(_parameters);
      sim.Complete(true);
      SimpleTimer.RunSteps(3600000);
      long memory = GC.GetTotalMemory(true);
      long time = (System.DateTime.UtcNow - rstart).Ticks / TimeSpan.TicksPerMillisecond;
      long throughput = (long) (SimulationEdgeListener.TotalDataTransferred() /
          (DateTime.UtcNow - vstart).TotalSeconds);
      sim.Disconnect();
      return new Result(memory, time, throughput);
    }
  }
}
