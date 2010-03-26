using NUnit.Framework;

namespace Brunet.Simulator {
  [TestFixture]
  public class TestSimulator {
    [Test]
    public void CompleteTheRing() {
      Parameters p = new Parameters("Test", "Test");
      string[] args = "-b=.2 -c -s=250".Split(' ');
      p.Parse(args);
      Simulator sim = new Simulator(p);
      Assert.IsTrue(sim.Complete(true), "Simulation failed to complete the ring");
    }
  }
}
