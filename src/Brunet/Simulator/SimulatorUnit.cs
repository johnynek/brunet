#if BRUNET_NUNIT
using Brunet;
using Brunet.Connections;
using Brunet.Util;
using NUnit.Framework;

namespace Brunet.Simulator {
  [TestFixture]
  public class TestSimulator {
    [Test]
    public void CompleteTheRing() {
      Parameters p = new Parameters("Test", "Test");
      string[] args = "-b=.2 -c -s=25".Split(' ');
      Assert.AreNotEqual(-1, p.Parse(args), "Unable to parse" + p.ErrorMessage);;
      Simulator sim = new Simulator(p);
      Assert.IsTrue(sim.Complete(true), "Simulation failed to complete the ring");
    }

    [Test]
    public void CompleteTheSecureRing() {
      Parameters p = new Parameters("Test", "Test");
      string[] args = "-b=.2 -c --secure_edges -s=25".Split(' ');
      Assert.AreNotEqual(-1, p.Parse(args), "Unable to parse" + p.ErrorMessage);;
      Simulator sim = new Simulator(p);
      Assert.IsTrue(sim.Complete(true), "Simulation failed to complete the ring");
    }

//    [Test]
    public void CompleteTheDtlsRing() {
      Parameters p = new Parameters("Test", "Test");
      string[] args = "-b=.2 --dtls -c --secure_edges -s=25".Split(' ');
      Assert.AreNotEqual(-1, p.Parse(args), "Unable to parse" + p.ErrorMessage);;
      Simulator sim = new Simulator(p);
      Assert.IsTrue(sim.Complete(true), "Simulation failed to complete the ring");
    }

    [Test]
    public void CompleteTheSubring() {
      SubringParameters p = new SubringParameters();
      string[] args = "-b=.2 -c --secure_edges -s=25 --subring=10".Split(' ');
      Assert.AreNotEqual(-1, p.Parse(args), "Unable to parse" + p.ErrorMessage);;
      SubringSimulator sim = new SubringSimulator(p);
      Assert.IsTrue(sim.Complete(true), "Simulation failed to complete the ring");
    }

    [Test]
    public void Relays() {
      Parameters p = new Parameters("Test", "Test");
      string[] args = "-s=100".Split(' ');
      Assert.AreNotEqual(-1, p.Parse(args), "Unable to parse" + p.ErrorMessage);;
      RelayOverlapSimulator sim = new RelayOverlapSimulator(p);

      Address addr1 = null, addr2 = null;
      sim.AddDisconnectedPair(out addr1, out addr2, sim.NCEnable);
      sim.Complete(true);
      SimpleTimer.RunSteps(1000000, false);

      Node node1 = (sim.Nodes[addr1] as NodeMapping).Node as Node;
      Node node2 = (sim.Nodes[addr2] as NodeMapping).Node as Node;

      if(node1.ConnectionTable.GetConnection(ConnectionType.Structured, addr2) != null) {
        Relays();
        return;
      }

      ManagedConnectionOverlord mco = new ManagedConnectionOverlord(node1);
      mco.Start();
      node1.AddConnectionOverlord(mco);
      mco.Set(addr2);
      sim.Complete(true);
      SimpleTimer.RunSteps(100000, false);

      Assert.IsTrue(node1.ConnectionTable.GetConnection(ConnectionType.Structured, addr2) != null);

      foreach(Connection con in node1.ConnectionTable.GetConnections(Relay.OverlapConnectionOverlord.STRUC_OVERLAP)) {
        con.State.Edge.Close();
      }
      foreach(Connection con in node2.ConnectionTable.GetConnections(Relay.OverlapConnectionOverlord.STRUC_OVERLAP)) {
        con.State.Edge.Close();
      }

      SimpleTimer.RunSteps(100000, false);
      Assert.IsTrue(node1.ConnectionTable.GetConnection(ConnectionType.Structured, addr2) != null);
      sim.Disconnect();
    }
  }
}
#endif
