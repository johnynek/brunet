using System;

namespace Brunet
{

/**
 * Everything that can go wrong with an edge is represented
 * by this exception
 */

  public class EdgeException:Exception
  {

    public EdgeException():base()
    {
    }
    public EdgeException(string message):base(message)
    {
    }
    public EdgeException(string mes, Exception inner):base(mes, inner)
    {
    }
  }

}
