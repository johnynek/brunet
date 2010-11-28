using Mono.Data.Sqlite;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

using Brunet.Connections;
using Brunet.Simulator.Tasks;
using Brunet.Simulator.Transport;
using Brunet.Transport;
using Brunet.Util;

namespace Brunet.Simulator.Regression {
  public class NatTest : RegressionTest {
    protected class Result {
      public readonly string TAType;
      public readonly long Time;
      public readonly double Throughput;
      
      public Result(string ta_type, long time, double throughput)
      {
        TAType = ta_type;
        Time = time;
        Throughput = throughput;
      }
    }

    protected delegate Result Test();
    protected Test[] _tests;

    public NatTest(RegressionParameters p) : base(p)
    {
      _tests = new Test[] { SymToRstAndAndOutOnlyToPublic, SymToRstAndAndOutOnlyToOutOnly, Restricted, Public, Cone };
    }

    override protected void RunTests()
    {
      foreach(Test test in _tests) {
        while(true) {
          Result result = test();
          if(result.Time != 0) {
            StoreTestResult(test, result);
            break;
          }
        }
      }
    }

    override protected void SetupResultTable()
    {
      IDbConnection dbcon = new SqliteConnection(_db_con);
      dbcon.Open();
      IDbCommand dbcmd = dbcon.CreateCommand();

      string sql = "CREATE TABLE nat_results (iter INTEGER, testid INTEGER, ta_type TEXT, time INTEGER, throughput REAL)";
      dbcmd.CommandText = sql;
      try {
        dbcmd.ExecuteNonQuery();
      } catch {
      }

      dbcmd.Dispose();
      dbcon.Close();
    }

    protected void StoreTestResult(Test test, Result result)
    {
      IDbConnection dbcon = new SqliteConnection(_db_con);
      dbcon.Open();
      IDbCommand dbcmd = dbcon.CreateCommand();

      string method = test.Method.Name;
      int testid = GetTestId(dbcmd, method);
      string sql = "INSERT INTO nat_results (iter, testid, ta_type, time, throughput) VALUES (" +
        CurrentIter + ", " + testid + ", \"" + result.TAType + "\", " + result.Time + ", " +
        result.Throughput + ")";
      dbcmd.CommandText = sql;
      dbcmd.ExecuteNonQuery();

      dbcmd.Dispose();
      dbcon.Close();
    }

    protected Result SymToRstAndAndOutOnlyToPublic()
    {
      Simulator sim = new Simulator(_parameters);
      Node node0 = NatFactory.AddNode(sim, NatTypes.Symmetric, NatTypes.OutgoingOnly, true);
      Node node1 = NatFactory.AddNode(sim, NatTypes.RestrictedCone, NatTypes.Public, true);
      return DoNatTest(sim, node0, node1);
    }

    protected Result SymToRstAndAndOutOnlyToOutOnly()
    {
      Simulator sim = new Simulator(_parameters);
      Node node0 = NatFactory.AddNode(sim, NatTypes.Symmetric, NatTypes.OutgoingOnly, true);
      Node node1 = NatFactory.AddNode(sim, NatTypes.RestrictedCone, NatTypes.OutgoingOnly, true);
      return DoNatTest(sim, node0, node1);
    }

    protected Result Public()
    {
      Simulator sim = new Simulator(_parameters);
      Node node0 = NatFactory.AddNode(sim, NatTypes.Public, NatTypes.Disabled, false);
      Node node1 = NatFactory.AddNode(sim, NatTypes.Public, NatTypes.Disabled, false);
      return DoNatTest(sim, node0, node1);
    }

    protected Result Cone()
    {
      Simulator sim = new Simulator(_parameters);
      Node node0 = NatFactory.AddNode(sim, NatTypes.Cone, NatTypes.Disabled, false);
      Node node1 = NatFactory.AddNode(sim, NatTypes.Cone, NatTypes.Disabled, false);
      return DoNatTest(sim, node0, node1);
    }

    protected Result Restricted()
    {
      Simulator sim = new Simulator(_parameters);
      Node node0 = NatFactory.AddNode(sim, NatTypes.RestrictedCone, NatTypes.Disabled, false);
      Node node1 = NatFactory.AddNode(sim, NatTypes.RestrictedCone, NatTypes.Disabled, false);
      return DoNatTest(sim, node0, node1);
    }

    protected Result DoNatTest(Simulator sim, Node node0, Node node1)
    {
      sim.Complete(true);
//      SimpleTimer.RunSteps(3600000);
      SimpleTimer.RunSteps(600000);
      long throughput = Throughput(node0.EdgeListenerList) + Throughput(node1.EdgeListenerList);
      DateTime start = DateTime.UtcNow;

      ManagedConnectionOverlord mco = new ManagedConnectionOverlord(node0);
      mco.Start();
      node0.AddConnectionOverlord(mco);
      mco.Set(node1.Address);

      AreConnected connected = new AreConnected(node0, node1, null);
      connected.Start();
      connected.Run(120);

      long time = (DateTime.UtcNow - start).Ticks / TimeSpan.TicksPerMillisecond;
      double avg_throughput = 0;
      if(time > 0) {
        throughput = Throughput(node0.EdgeListenerList) +
          Throughput(node1.EdgeListenerList) - throughput;
        avg_throughput = (1.0 * throughput) / time / 1000;
      }

      sim.Disconnect();
      return new Result(connected.TATypeAsString, time, avg_throughput);
    }

    protected long Throughput(IEnumerable edge_listeners)
    {
      long data_transfered = 0;
      foreach(EdgeListener el in edge_listeners) {
        SimulationEdgeListener simel = el as SimulationEdgeListener;
        if(simel != null) {
          data_transfered += simel.BytesSent;
        }
      }
      return data_transfered;
    }
  }
}
