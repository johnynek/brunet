namespace Brunet.Tftp {

public class Error  {

  /**
   * These error codes are define in the TFTP spec
   * @see http://www.faqs.org/rfcs/rfc1350.html
   */
  public enum Code : short {
    NotDefined = 0,
    FileNotFound = 1,
    AccessViolation = 2,
    DiskFull = 3,
    IllegalOp = 4,
    UnknownTID = 5,
    FileAlreadyExists = 6,
    NoSuchUser = 7
  }
	
  public Error(Code code, string message) {

  }

  protected Code _code;
  public Code EC { get { return _code; } }
	
  protected string _message;
  public string Message { get { return _message; } }


  /**
   * Write out the error code and the message into
   * the given byte buffer.
   * @return the number of bytes written
   */
  public int CopyTo(byte[] target, int offset) {
    return 0;
  }
}
	
}
