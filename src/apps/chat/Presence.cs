namespace Brunet.Chat {

[System.Xml.Serialization.XmlTypeAttribute("presence")]
public class Presence {

  private string _from;
  [System.Xml.Serialization.XmlAttributeAttribute("from")]
  public string From {
    get { return _from; }
    set { _from = value; }
  }
  private string _to;
  [System.Xml.Serialization.XmlAttributeAttribute("to")]
  public string To {
    get { return _to; }
    set { _to = value; }
  }
  private string _type;
  [System.Xml.Serialization.XmlAttributeAttribute("type")]
  public string PresType {
    get { return _type; }
    set { _type = value; }
  }
  private string _show;
  [System.Xml.Serialization.XmlElementAttribute("show")]
  public string Show {
    get { return _show; }
    set { _show = value; }
  }
  private string _status;
  [System.Xml.Serialization.XmlElementAttribute("status")]
  public string Status {
    get { return _status; }
    set { _status = value; }
  }
  public Presence() { }

  /**
   * This inner class holds some constants
   */
  public class ShowValues {
    public static readonly string Away = "away";
    public static readonly string Chat = "chat";
    public static readonly string Dnd = "dnd";
    public static readonly string Xa = "xa";
  }
  public class TypeValues {
    public static readonly string Error = "error";
    public static readonly string Probe = "probe";
    public static readonly string Subscribe = "subscribe";
    public static readonly string Subscribed = "subscribed";
    public static readonly string Unavailable = "unavailable";
    public static readonly string Unsubscribe = "unsubscribe";
    public static readonly string Unsubscribed = "unsubscribed";

  }
}

}
