using System.Xml.Serialization;

namespace Brunet.Chat {

[System.Xml.Serialization.XmlTypeAttribute("message")]
public class Message {

  private string _from;
  [System.Xml.Serialization.XmlAttributeAttribute("from")]
  public string From {
    get { return _from; }
    set { _from = value; }
  }
  private string _id;
  [System.Xml.Serialization.XmlAttributeAttribute("id")]
  public string Id {
    get { return _id; }
    set { _id = value; }
  }

  private string _to;
  [System.Xml.Serialization.XmlAttributeAttribute("to")]
  public string To {
    get { return _to; }
    set { _to = value; }
  }
  private string _body;
  [System.Xml.Serialization.XmlElementAttribute("body")]
  public string Body {
    get { return _body; }
    set { _body = value; }
  }
  public Message() { }
	
	
}

}
