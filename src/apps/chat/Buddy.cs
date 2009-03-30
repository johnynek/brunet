using System.Xml;
using System.Xml.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Brunet{

/**
 * Represents a Buddy in the Chat client
 */
public class Buddy
{
  private string alias;
  private string email;
  
  [XmlAttribute("Alias")] 
  public string Alias{
    get{
      return alias;
    }
    set{
      if(null!=value)
        alias = value;
    }
  }	  
  
  [XmlAttribute("Email")] 
  public string Email{
    get{
      return email;
    }
    set{
      if(null!=value)
        email = value;
    }
  } 

  protected string _status;
  /*
   * The status of this Buddy
   */
  [XmlIgnore]
  public string Status {
    get {
      return _status;
    }
    set {
      if( _status != value ) {
        _status = value;
        if( StatusChanged != null ) {
          StatusChanged(this, System.EventArgs.Empty);
        }
      }
    }
  }
  protected User _user;
  [XmlIgnore]
  public User User {
    get { return _user; }
    set { _user = value; }
  }
	  
  public event System.EventHandler StatusChanged;
 
  protected Brunet.Chat.Presence _pres;
  [XmlIgnore]
  public Brunet.Chat.Presence Presence {
    get { return _pres; }
    set {
      _pres = value;
      if( _pres.PresType == Brunet.Chat.Presence.TypeValues.Unsubscribed ) {
        this.Status = _pres.PresType;
      }
      else if( _pres.PresType == Brunet.Chat.Presence.TypeValues.Unavailable ) {
        this.Status = _pres.PresType;
      }
      else {
        this.Status = _pres.Show;
      }
    }
  }
  
  protected Node _node;
  [XmlIgnore]
  public Node Node {
    get {
      return _node;
    }
    set {
      _node = value;
    }
  }
  /*
   * The Brunet Address for this Buddy
   */
  protected ISender _sender;
  public ISender Sender {
    get {
      if( null == _sender && Email != "") { 
        SHA1 sha = new SHA1CryptoServiceProvider();  
        byte[] budhashedemail = sha.ComputeHash(Encoding.UTF8.GetBytes(Email));
        //inforce type 0
        Address.SetClass(budhashedemail, 0);
        var addr = AddressParser.Parse(budhashedemail);
        _sender = new AHExactSender(_node, addr);
      }
      return _sender;
    }
  }
  
  
  public Buddy(){
    _status = Brunet.Chat.Presence.TypeValues.Unavailable;
  }
  
  public Buddy(string a_alias,string a_email)
  {
    Alias = a_alias;
    Email = a_email;
    _status = Brunet.Chat.Presence.TypeValues.Unavailable;
  }

  /**
   * Send a message to this buddy.
   */
  public void SendMessageText(string sendtext)
  {
    /*
     * Prepare a Message object
     */
    XmlSerializer mser = new XmlSerializer(typeof(Brunet.Chat.Message));
    System.IO.StringWriter sw = new System.IO.StringWriter();
    XmlWriter w = new XmlTextWriter(sw);
    Brunet.Chat.Message m = new Brunet.Chat.Message();
    m.Body = sendtext;
    mser.Serialize(w, m);
    Channel results = new Channel(1);
    results.CloseEvent += delegate(object q, System.EventArgs ce) {
      if( Status == Brunet.Chat.Presence.TypeValues.Unavailable ) {
        //We are getting packets from an offline node, double check:
        SendPresence();
      }
    };
    _node.Rpc.Invoke(Sender, results, "example:chat.message", sw.ToString());
  }
  /**
   * Send and request latest presence info for this Buddy:
   */
  public void SendPresence() {
    XmlSerializer ser = new XmlSerializer(typeof(Brunet.Chat.Presence));
    System.IO.StringWriter sw = new System.IO.StringWriter();
    XmlWriter w = new XmlTextWriter(sw);
    Brunet.Chat.Presence p = new Brunet.Chat.Presence();
    p.Show = User.Show;
    if( p.Show == Brunet.Chat.Presence.TypeValues.Unavailable ) {
      p.PresType = Brunet.Chat.Presence.TypeValues.Unavailable;
    }
    p.Status = User.Status;
    ser.Serialize(w, p);
    //Wait for one result:
    Channel results = new Channel(1);
    results.CloseEvent += delegate(object q, System.EventArgs args) {
      try {
        RpcResult res = results.Dequeue() as RpcResult;
        string p_str = (string)res.Result;
        XmlReader r = new XmlTextReader(new System.IO.StringReader(p_str));
        XmlSerializer pser = new XmlSerializer(typeof(Brunet.Chat.Presence));

        this.Presence = (Brunet.Chat.Presence)pser.Deserialize(r);
      }
      catch {
        //I guess this buddy is offline:
        Status = Brunet.Chat.Presence.TypeValues.Unavailable;
      }
    };
    _node.Rpc.Invoke(Sender, results, "example:chat.presence", sw.ToString());
  }
}

}
