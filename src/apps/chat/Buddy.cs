using System.Xml;
using System.Xml.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Brunet{

/**
 * Represents a Buddy in the Chat client
 */
public class Buddy : IReplyHandler
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
  
  protected ReqrepManager _rrman;
  [XmlIgnore]
  public ReqrepManager RRMan {
    get {
      return _rrman;
    }
    set {
      _rrman = value;
    }
  }
  /*
   * The Brunet Address for this Buddy
   */
  protected Address _add;
  public Address Address {
    get {
      if( null == _add && Email != "") { 
        SHA1 sha = new SHA1CryptoServiceProvider();  
        byte[] budhashedemail = sha.ComputeHash(Encoding.UTF8.GetBytes(Email));
        //inforce type 0
        budhashedemail[Address.MemSize - 1] &= 0xFE;
        _add = new AHAddress(budhashedemail);
      }
      return _add;
    }
  }
  
  protected System.Collections.Hashtable _requests;
  protected object _sync;
  
  public Buddy(){
    _status = Brunet.Chat.Presence.TypeValues.Unavailable;
    _requests = new System.Collections.Hashtable();
    _sync = new System.Object();
  }
  
  public Buddy(string a_alias,string a_email)
  {
    Alias = a_alias;
    Email = a_email;
    _status = Brunet.Chat.Presence.TypeValues.Unavailable;
    _requests = new System.Collections.Hashtable();
    _sync = new System.Object();
  }

  //Implements IReplyHandler.HandleError
  public void HandleError(ReqrepManager man, int req_num, ReqrepManager.ReqrepError er,
                          object state)
  {
    /** @todo we should tell the user the message was not delivered */
    object o = null;
    lock( _sync ) {
      o = _requests[req_num]; 
      _requests.Remove(req_num);
    }
    //This buddy had an error, unless it was about Presence, we should check
    //presence:
    if( o is Brunet.Chat.Presence ) {
      //I guess this buddy is offline:
      Status = Brunet.Chat.Presence.TypeValues.Unavailable;
    }
    else {
      //Looks like a non-presence failure.  Now lets check presence.
      SendPresence();
    }
  }
  
  //Here we deal with the reply to our chats.  In fact, the replies
  //are currently empty and only used so we know that the chat made it
  //to the recipient
  public bool HandleReply(ReqrepManager man, ReqrepManager.ReqrepType rt,
                  int mid, string prot, System.IO.MemoryStream payload,
                  AHPacket packet, object state)
  {
    /**
     * @todo we may want to add some feedback to the user that the message
     * was recieved
     */
    if( packet.Source.Equals( User.Address ) ) {
      //Ignore our own replies...
      return true;
    }
    XmlReader r = new XmlTextReader(payload);
    XmlSerializer mser = new XmlSerializer(typeof(Brunet.Chat.Message));
    XmlSerializer pser = new XmlSerializer(typeof(Brunet.Chat.Presence));
    try {
     if( mser.CanDeserialize(r) ) {
      //This is a message
     }
     else if( pser.CanDeserialize(r) ) {
      //This is presence (I love presence)
      /*
       * This is the reply to our presence message.
       * We don't need to send a reply to this.
       */
      Brunet.Chat.Presence p = (Brunet.Chat.Presence)pser.Deserialize(r);
      this.Presence = p;
     }
    }
    catch(System.Xml.XmlException x) {
      //Looks like nothing is in the packet...
    }
    /*
     * We got a response, check to see if this Buddy is not offline
     */
    if( Status == Brunet.Chat.Presence.TypeValues.Unavailable ) {
      //We are getting packets from an offline node, double check:
      SendPresence();
    }
    object o = null;
    lock( _sync ) {
      o =_requests[mid]; 
      _requests.Remove(mid);
    }
    //Stop listening for further responses:
    return false;
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
    System.IO.MemoryStream ms = new System.IO.MemoryStream();
    XmlWriter w = new XmlTextWriter(ms, System.Text.Encoding.UTF8);
    Brunet.Chat.Message m = new Brunet.Chat.Message();
    m.Body = sendtext;
    mser.Serialize(w, m);
    lock( _sync ) {
      int req_num = RRMan.SendRequest( this.Address,
                                        ReqrepManager.ReqrepType.Request,
                                        AHPacket.Protocol.Chat,
                                        ms.ToArray(),
                                        this,
                                        null);
      _requests[req_num] = m;
    }
  }
  /**
   * Send and request latest presence info for this Buddy:
   */
  public void SendPresence() {
    XmlSerializer ser = new XmlSerializer(typeof(Brunet.Chat.Presence));
    System.IO.MemoryStream ms = new System.IO.MemoryStream();
    XmlWriter w = new XmlTextWriter(ms, System.Text.Encoding.UTF8);
    Brunet.Chat.Presence p = new Brunet.Chat.Presence();
    p.From = User.Address.ToString();
    p.To = Address.ToString();
    p.Show = User.Show;
    if( p.Show == Brunet.Chat.Presence.TypeValues.Unavailable ) {
      p.PresType = Brunet.Chat.Presence.TypeValues.Unavailable;
    }
    p.Status = User.Status;
    ser.Serialize(w, p);
    lock( _sync ) {
      int req = RRMan.SendRequest( this.Address, ReqrepManager.ReqrepType.Request,
                                        AHPacket.Protocol.Chat,
                                        ms.ToArray(),
                                        this,
                                        null);
      _requests[req] = p;
    }
  }
}

}
