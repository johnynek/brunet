using System;

namespace Brunet {

  /**
   * Lots of things can go wrong in the Linker.  When something
   * goes wrong, the Linker throws a LinkException.
   *
   * All LinkExceptions should be caught inside Linker.
   */
  public class LinkException : Exception {
    public LinkException():base()
    {
    }
    public LinkException(string message):base(message)
    {
    }
    public LinkException(string mes, Exception inner):base(mes, inner)
    {
    }

  }
}
