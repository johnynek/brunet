//using Brunet.ConnectionMessage;
using Brunet;

namespace Brunet
{

  /**
   * The close message is sent and acknowledged
   * when a connection is to be closed
   */
  public class CloseMessage:ConnectionMessage
  {

    public CloseMessage()
    {
      _reason = "";
    }
    /**
     * Make a close message with a non-empty reason string
     */
    public CloseMessage(string reason)
    {
      _reason = reason;
    }

    public CloseMessage(System.Xml.XmlElement close_element)
    {
      //Get the reason:
      _reason = "";
      if( close_element.FirstChild != null )
        if( close_element.FirstChild.Value != null )
          _reason = close_element.FirstChild.Value;
    }

    protected string _reason;
    public string Reason {
    get { return _reason; }
    }
    override public void WriteTo(System.Xml.XmlWriter w)
    {

      base.WriteTo(w);  //<(request|response)>
      string xml_ns = "";
      w.WriteStartElement("close", xml_ns);     //<close>
      if( _reason.Length > 0 ) {
        w.WriteString( _reason );
      }
      w.WriteEndElement();      //</close>
      w.WriteEndElement();      //</(request|response)>
    }
  }

}
