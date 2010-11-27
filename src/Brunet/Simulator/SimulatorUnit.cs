#if BRUNET_NUNIT
using System;
using Brunet;
using Brunet.Connections;
using Brunet.Util;
using Brunet.Simulator.Tasks;
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

      TestNat(sim, NatTypes.Cone, NatTypes.Disabled, false);
      TestNat(sim, NatTypes.RestrictedCone, NatTypes.Disabled, false);
      TestNat(sim, NatTypes.Symmetric, NatTypes.Disabled, true);
      TestNat(sim, NatTypes.Symmetric, NatTypes.Disabled, NatTypes.RestrictedCone, NatTypes.Disabled, false);
      TestNat(sim, NatTypes.Symmetric, NatTypes.OutgoingOnly, true);
    }

    private void TestNat(Simulator sim, NatTypes type0, NatTypes type1, bool relay)
    {
      TestNat(sim, type0, type1, type0, type1, relay);
    }

    private void TestNat(Simulator sim, NatTypes n0type0, NatTypes n0type1,
        NatTypes n1type0, NatTypes n1type1, bool relay)
    {
      string fail_s = String.Format("{0}/{1} and {2}/{3}", n0type0, n0type1,
          n1type0, n1type1);
      Node node0 = null;
      Node node1 = null;
      while(true) {
        node0 = NatFactory.AddNode(sim, n0type0, n0type1, relay);
        node1 = NatFactory.AddNode(sim, n1type0, n1type1, relay);

        Assert.IsTrue(sim.Complete(true), fail_s + " nodes are connected to the overlay");
        if(!Simulator.AreConnected(node0, node1)) {
          break;
        }
      }

      ManagedConnectionOverlord mco = new ManagedConnectionOverlord(node0);
      mco.Start();
      node0.AddConnectionOverlord(mco);
      mco.Set(node1.Address);

      Assert.IsTrue(AreConnected(node0, node1), fail_s + " nodes were unable to connect.");
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

        if(!Simulator.AreConnected(node1, node2)) {
          break;
        }
      }

      ManagedConnectionOverlord mco = new ManagedConnectionOverlord(node1);
      mco.Start();
      node1.AddConnectionOverlord(mco);
      mco.Set(addr2);
      Assert.IsTrue(AreConnected(node1, node2));

      foreach(Connection con in node1.ConnectionTable.GetConnections(Relay.OverlapConnectionOverlord.STRUC_OVERLAP)) {
        con.State.Edge.Close();
      }
      foreach(Connection con in node2.ConnectionTable.GetConnections(Relay.OverlapConnectionOverlord.STRUC_OVERLAP)) {
        con.State.Edge.Close();
      }

      Assert.IsTrue(Simulator.AreConnected(node1, node2));
    }

    protected bool AreConnected(Node node0, Node node1)
    {
      Task connected = new AreConnected(node0, node1, null);
      connected.Start();
      connected.Run(120);
      return connected.Done;
    }
  }
}
#endif
