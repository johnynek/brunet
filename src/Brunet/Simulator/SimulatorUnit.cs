#if BRUNET_NUNIT
using Brunet;
using Brunet.Connections;
using Brunet.Util;
using Brunet.Simulator.Transport;
using NUnit.Framework;

namespace Brunet.Simulator {
  [TestFixture]
  public class TestSimulator {
    private Simulator _sim;
    [TearDown]
    public void Cleanup()
    {
      if(_sim != null) {
        _sim.Disconnect();
        _sim = null;
      }
    }

    [Test]
    public void CompleteTheRing() {
      Parameters p = new Parameters("Test", "Test");
      string[] args = "-b=.2 -c -s=25".Split(' ');
      Assert.AreNotEqual(-1, p.Parse(args), "Unable to parse" + p.ErrorMessage);;
      Simulator sim = new Simulator(p);
      _sim = sim;
      Assert.IsTrue(sim.Complete(true), "Simulation failed to complete the ring");
    }

    [Test]
    public void CompleteTheSecureRing() {
      Parameters p = new Parameters("Test", "Test");
      string[] args = "-b=.2 -c --secure_edges -s=25".Split(' ');
      Assert.AreNotEqual(-1, p.Parse(args), "Unable to parse" + p.ErrorMessage);;
      Simulator sim = new Simulator(p);
      _sim = sim;
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
      _sim = sim;
      Assert.IsTrue(sim.Complete(true), "Simulation failed to complete the ring");
    }

    [Test]
    public void TestNatTraversal() {
      Parameters p = new Parameters("Test", "Test");
      string[] args = "-c -s=100".Split(' ');
      Assert.AreNotEqual(-1, p.Parse(args), "Unable to parse" + p.ErrorMessage);;
      Simulator sim = new Simulator(p);
      _sim = sim;
      Assert.IsTrue(sim.Complete(true), "Simulation failed to complete the ring");
      SimpleTimer.RunSteps(1000000, false);

      Node node0 = null;
      Node node1 = null;
      while(true) {
        node0 = sim.AddNode();
        node1 = sim.AddNode();

        NatFactory.AddNat(node0.EdgeListenerList, NatTypes.Cone);
        NatFactory.AddNat(node1.EdgeListenerList, NatTypes.Cone);
        Assert.IsTrue(sim.Complete(true), "Cone nodes connected to the overlay");
        if(!IsConnected(node0, node1.Address)) {
          break;
        }
      }

      ManagedConnectionOverlord mco = new ManagedConnectionOverlord(node0);
      mco.Start();
      node0.AddConnectionOverlord(mco);
      mco.Set(node1.Address);

      Assert.IsTrue(IsConnected(node0, node1.Address), "NAT nodes were unable to form a direct connection!");

      while(true) {
        node0 = sim.AddNode();
        node1 = sim.AddNode();

        NatFactory.AddNat(node0.EdgeListenerList, NatTypes.RestrictedCone);
        NatFactory.AddNat(node1.EdgeListenerList, NatTypes.RestrictedCone);
        Assert.IsTrue(sim.Complete(true), "RestrictedCone nodes connected to the overlay");
        if(!IsConnected(node0, node1.Address)) {
          break;
        }
      }

      mco = new ManagedConnectionOverlord(node0);
      mco.Start();
      node0.AddConnectionOverlord(mco);
      mco.Set(node1.Address);

      Assert.IsTrue(IsConnected(node0, node1.Address), "NAT nodes were unable to form a direct connection!");
    }

    [Test]
    public void Relays() {
      Parameters p = new Parameters("Test", "Test");
      string[] args = "-s=100".Split(' ');
      Assert.AreNotEqual(-1, p.Parse(args), "Unable to parse" + p.ErrorMessage);;
      RelayOverlapSimulator sim = new RelayOverlapSimulator(p);
      _sim = sim;

      Address addr1 = null, addr2 = null;
      Node node1 = null, node2 = null;
      while(true) {
        sim.AddDisconnectedPair(out addr1, out addr2, sim.NCEnable);
        sim.Complete(true);

        node1 = (sim.Nodes[addr1] as NodeMapping).Node as Node;
        node2 = (sim.Nodes[addr2] as NodeMapping).Node as Node;

        if(!IsConnected(node1, addr2)) {
          break;
        }
      }

      ManagedConnectionOverlord mco = new ManagedConnectionOverlord(node1);
      mco.Start();
      node1.AddConnectionOverlord(mco);
      mco.Set(addr2);
      Assert.IsTrue(IsConnected(node1, addr2));

      foreach(Connection con in node1.ConnectionTable.GetConnections(Relay.OverlapConnectionOverlord.STRUC_OVERLAP)) {
        con.State.Edge.Close();
      }
      foreach(Connection con in node2.ConnectionTable.GetConnections(Relay.OverlapConnectionOverlord.STRUC_OVERLAP)) {
        con.State.Edge.Close();
      }

      Assert.IsTrue(IsConnected(node1, addr2));
    }

    protected bool IsConnected(Node node, Address other)
    {
      DateTime start = DateTime.UtcNow;
      long ticks_end = start.AddMinutes(2).Ticks;
      bool success = false;
      while(DateTime.UtcNow.Ticks < ticks_end) {
        success = node.ConnectionTable.GetConnection(ConnectionType.Structured, other) != null;
        if(success) {
          break;
        }
        SimpleTimer.RunStep();
      }

      return success;
    }
  }
}
#endif
