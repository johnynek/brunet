/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2006 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

using System;
using System.Collections;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet
{

  /**
   * Anytime there is an error in the Connection protocol, this message may
   * be sent.
   * 
   * In particular:
   * When a node that is already connected requests a new connection of the same type,
   * this ErrorMessage is sent to that node indicating error.  Note that multiple 
   * connections of the same type (both structured or both unstructured or both leaf)
   * is not allowed between the same pair of nodes.
   *
   * When a node recieves a ConnectToMessage, the CtmRequestHandler or the Connector
   * checks to see if the target address is already in the connection table of the
   * same type.  If a connection already exists an ErrorMessage is sent back to the 
   * request node.  
   *
   * @see CtmRequestHandler
   * @see Connector
   * 
   */
  public class ErrorMessage
  {

    /**
     * @param ec The ErrorCode for this message
     * @param message the string in the message
     */
    public ErrorMessage(ErrorMessage.ErrorCode ec, string message)
    {
      _ec = ec;;
      _message = message;
    }

    public ErrorMessage()
    {
    }

    public override bool Equals(object o)
    {
      ErrorMessage eo = o as ErrorMessage;
      if( eo != null ) {
        bool same = true;
	same &= eo.Ec == _ec;
	same &= eo.Message == _message;
	return same;
      }
      else {
        return false;
      }
    }
    override public int GetHashCode() {
      return (int)this.Ec;
    }
    
  public enum ErrorCode : int
    {
      ErrorAck = 0, //Used to acknowledge an error request message.
      UnexpectedRequest = 1,
      UnknownConnectionType = 2,
      RealmMismatch = 3,
      ConnectToSelf = 16,
      InProgress = 17, //When we are in the process of connecting, don't allow a second
                       //from the same node.
      AlreadyConnected = 18,
      TargetMismatch = 19,
      Disconnecting = 20,
      EdgeClosed = 21,
      BadXml = 2000,
      SpecViolation = 2001,
      UnknownConnectionMessage = 2002,
      DiscretionaryConnectionRefusal = 2003
    }

    protected ErrorCode _ec;
    public ErrorCode Ec {
      get { return _ec; }
    }

    protected string _message;
    public string Message {
      get { return _message; }
    }

  }
#if BRUNET_NUNIT

  [TestFixture]
  public class ErrorMessageTester {
    public ErrorMessageTester() { }

    [Test]
    public void EMTest()
    {
      ErrorMessage em1 = new ErrorMessage(ErrorMessage.ErrorCode.UnexpectedRequest,
		                          "Who are you???");

      ErrorMessage em2 = new ErrorMessage(ErrorMessage.ErrorCode.AlreadyConnected, "We are BFF");
    }
  }
#endif
  
}


