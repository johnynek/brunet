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
  public string Status {
    get {
      return _status;
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
  
  public Buddy(){
    _status = "online";
  }
  
  public Buddy(string a_alias,string a_email)
  {
    Alias = a_alias;
    Email = a_email;
    _status = "online";
  }

  //Implements IReplyHandler.HandleError
  public void HandleError(ReqrepManager man, int req_num, ReqrepManager.ReqrepError er,
                          object state)
  {
    /** @todo we should tell the user the message was not delivered */
    
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
    //Stop listening for further responses:
    return false;
  }
  
  /**
   * Send a message to this buddy.
   */
  public void SendMessageText(string sendtext)
  {
    //Console.WriteLine("sending {0}",sendtext);
    byte[] payload = Encoding.UTF8.GetBytes(sendtext);
    int req_num = RRMan.SendRequest( this.Address,
                                        ReqrepManager.ReqrepType.Request,
                                        AHPacket.Protocol.Chat,
                                        payload,
                                        this,
                                        null);
  }


}

}
