/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

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
