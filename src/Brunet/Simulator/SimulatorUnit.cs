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

    [Test]
    public void CompleteTheDtlsRing() {
      Parameters p = new Parameters("Test", "Test");
      string[] args = "-b=.2 --dtls -c --secure_edges -s=25".Split(' ');
      Assert.AreNotEqual(-1, p.Parse(args), "Unable to parse" + p.ErrorMessage);;
      Simulator sim = new Simulator(p);
      Assert.IsTrue(sim.Complete(true), "Simulation failed to complete the ring");
    }
  }
}
