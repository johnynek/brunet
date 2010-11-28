using Mono.Data.Sqlite;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

using Brunet.Simulator.Transport;
using Brunet.Util;

namespace Brunet.Simulator.Regression {
  public abstract class RegressionTest {
    readonly protected string _db_con;
    private int _current_iter;
    public int CurrentIter { get { return _current_iter; } }
    protected RegressionParameters _parameters;

    public RegressionTest(RegressionParameters p)
    {
      _parameters = p;
      _db_con = String.Format("URI=file:{0},version=3", p.Output);
      InitializeDatabase();
      SetupResultTable();
    }

    virtual public void Start()
    {
      for(int i = 0; i < _parameters.Iterations; i++) {
        NextIter();
        RunTests();
      }
    }

    /// <summary>Execute all the tests and store the results into the database.</summary>
    abstract protected void RunTests();
    /// <summary>The table where all results will be stored.</summary>
    abstract protected void SetupResultTable();

    /// <summary>Creates the database if necessary and returns the current
    /// iteration.</summary>
    private void InitializeDatabase()
    {
      IDbConnection dbcon = new SqliteConnection(_db_con);
      dbcon.Open();
      IDbCommand dbcmd = dbcon.CreateCommand();

      string sql = "CREATE TABLE tests (testid INTEGER PRIMARY KEY AUTOINCREMENT, test TEXT)";
      dbcmd.CommandText = sql;
      try {
        dbcmd.ExecuteNonQuery();
      } catch {
      }

      sql = "CREATE TABLE iterations (iter INTEGER PRIMARY KEY AUTOINCREMENT, date INTEGER)";
      dbcmd.CommandText = sql;
      try {
        dbcmd.ExecuteNonQuery();
      } catch {
      }

      dbcmd.Dispose();
      dbcon.Close();
    }

    protected void NextIter()
    {
      IDbConnection dbcon = new SqliteConnection(_db_con);
      dbcon.Open();
      IDbCommand dbcmd = dbcon.CreateCommand();

      string sql = "INSERT INTO iterations (date) VALUES (datetime())";
      dbcmd.CommandText = sql;
      dbcmd.ExecuteNonQuery();

      sql = "SELECT MAX(iter) FROM iterations";
      dbcmd.CommandText = sql;
      _current_iter = Int32.Parse(dbcmd.ExecuteScalar().ToString());

      dbcmd.Dispose();
      dbcon.Close();
    }

    /// <summary>Get the ID of a test if it exists or -1.</summary>
    protected int GetTestId(IDbCommand dbcmd, string test)
    {
      string ssql = "SELECT testid FROM tests WHERE test == \"" + test + "\"";
      dbcmd.CommandText = ssql;
      try {
        return Int32.Parse(dbcmd.ExecuteScalar().ToString());
      } catch {
      }

      string isql = "INSERT INTO tests (test) VALUES (\"" + test + "\")";
      dbcmd.CommandText = isql;
      dbcmd.ExecuteNonQuery();

      dbcmd.CommandText = ssql;
      return Int32.Parse(dbcmd.ExecuteScalar().ToString());
    }
  }
}
