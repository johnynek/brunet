namespace Brunet
{

/**
 * Represents a problem parsing some input or network
 * data in the Brunet system
 */
  using System;

  public class ParseException:Exception
  {
    public ParseException():base()
    {
    }
    public ParseException(string message):base(message)
    {
    }
    public ParseException(string message, Exception inner)
    : base(message, inner)
    {
    }
  }

}
