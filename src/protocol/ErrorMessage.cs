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

/*
 * Dependencies
 * Brunet.ConnectionMessage
 */

using System.Xml;
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
   * ErrorMessages are represented in xml like this:
   * <error code="number">error message here</error>
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
  public class ErrorMessage:ConnectionMessage
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

    /**
      * Deserializes the ErrorCode element
      */
    public ErrorMessage(System.Xml.XmlElement r) : base(r)
    {
      XmlElement encoded = (XmlElement)r.FirstChild;
      //Read the attributes of the ErrorCode
      foreach(XmlNode attr in((XmlElement) encoded).Attributes)
      {
        switch (attr.Name) {
        case "code":
          _ec = (ErrorCode) System.Enum.Parse(typeof(ErrorCode),
                                              attr.FirstChild.Value,
                                              true);
          break;
        }
      }
      //Get the message:
      _message = encoded.FirstChild.Value;
    }

    public ErrorMessage()
    {
    }

    public override bool CanReadTag(string tag)
    {
      return (tag == "error");
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
    
    public override IXmlAble ReadFrom(XmlElement el)
    {
      return new ErrorMessage(el);
    }
    
    public override void WriteTo(XmlWriter w)
    {
      //Write the request or response and id
      base.WriteTo(w);  //<(request|response)>

      //then write this: <error code="12">Already connected</error>

      string ns = "";
      //Here we write out the specific stuff :
      w.WriteStartElement("error", ns);     //<error>
      //Write the attributes :
      w.WriteStartAttribute("code", ns);
      w.WriteString( ((int)Ec).ToString() ); //put in the appropriate code
      w.WriteEndAttribute();
      w.WriteString(_message);
      w.WriteEndElement();      //</error>
      w.WriteEndElement();      //</(request|response)>
    }

  public enum ErrorCode : int
    {
      ErrorAck = 0, //Used to acknowledge an error request message.
      UnexpectedRequest = 1,
      UnknownConnectionType = 2,
      ConnectToSelf = 16,
      InProgress = 17, //When we are in the process of connecting, don't allow a second
                       //from the same node.
      AlreadyConnected = 18,
      BadXml = 2000,
      SpecViolation = 2001,
      UnknownConnectionMessage = 2002
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
      XmlAbleTester xt = new XmlAbleTester();
      ErrorMessage em1 = new ErrorMessage(ErrorMessage.ErrorCode.UnexpectedRequest,
		                          "Who are you???");
      ErrorMessage em1a = (ErrorMessage)xt.SerializeDeserialize(em1);
      Assert.AreEqual(em1, em1a);

      ErrorMessage em2 = new ErrorMessage(ErrorMessage.ErrorCode.AlreadyConnected, "We are BFF");
      ErrorMessage em2a = (ErrorMessage)xt.SerializeDeserialize(em2);
      Assert.AreEqual(em2, em2a);
    }
  }
#endif
  
}


