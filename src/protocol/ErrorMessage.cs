/*
 * Dependencies
 * Brunet.ConnectionMessage
 */

using System.Xml;
using System.Collections;

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
    public ErrorMessage(System.Xml.XmlElement encoded)
    {
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
      UnexpectedRequest = 1,
      AlreadyConnected = 18
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

}


