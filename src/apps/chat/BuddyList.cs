using System;	
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;

namespace Brunet{

[XmlRootAttribute("BuddyList")]
public class BuddyList : IRequestHandler
{
  private ArrayList buddyArrayList;
  
  public BuddyList()
  {
    buddyArrayList = new ArrayList();
    _email_to_buddy = new Hashtable();
    _add_to_buddy = new Hashtable();
  }

  [XmlArray(ElementName="Buddies")]
  [XmlArrayItem(ElementName="Buddy",Type=typeof(Buddy))]
  public Buddy[] Buddies {
    get{
      Buddy[] buddies = new Buddy[buddyArrayList.Count];
      buddyArrayList.CopyTo(buddies);
      return buddies;
    }
    set{
      if (null == value )  {
	Console.WriteLine("Setting Null Buddies");
	return;
      }
      Clear();
      Console.Write("Setting Buddies");
      foreach (Buddy bud in value) {
	Console.WriteLine("Address: {0} Email: {1}", bud.Address, bud.Email);
	Add(bud);
      }
    }
  }
 
  protected User _user;
  [XmlIgnore]
  public User User {
    get { return _user; }
    set {
      if( _user == null ) {
        //Only set this once:
        _user = value;
        _user.Changed += this.UserHandler;
      }
    }
  }
  /**
   * When a chat message is received, this event is
   * fired
   */
  public event EventHandler ChatEvent;
        
  protected Hashtable _add_to_buddy;
  protected Hashtable _email_to_buddy;
  
  public int Add(Buddy bud){
    if( !Contains(bud) && (bud.Address != null) ) {
      _add_to_buddy[ bud.Address ] = bud;
      _email_to_buddy[ bud.Email ] = bud;
      return buddyArrayList.Add(bud);
    }
    return 0;
  }
  public void Clear(){
    buddyArrayList.Clear();
    _add_to_buddy.Clear();
    _email_to_buddy.Clear();
  }

  public bool Contains(Buddy b)
  {
    if (b.Address == null ) {
      return false;
    }
    return _add_to_buddy.ContainsKey(b.Address);
  }
  /**
   * @return the buddy in the list that has the given Address
   */
  public Buddy GetBuddyWithAddress(Address a)
  {
    return (Buddy)_add_to_buddy[a];
  }

  public Buddy GetBuddyWithEmail(string email)
  {
    return (Buddy) _email_to_buddy[email];
  }

  /**
   * This is for foreach support.  Just returns
   * the iterator for the underlying list
   */
  public IEnumerator GetEnumerator()
  {
    return buddyArrayList.GetEnumerator(); 
  }

    /*
     * This is a request of us.
     */
    public void HandleRequest(ReqrepManager man, ReqrepManager.ReqrepType rt,
		              object req, string prot,
			      System.IO.MemoryStream payload, AHPacket packet)
    {
      /*
       * Ignore requests from ourselves:
       */
      if( packet.Source.Equals( User.Address ) ||
	  !packet.Destination.Equals( User.Address ) ) {
	//Ignore these requests
        return;
      }
      XmlReader r = new XmlTextReader(payload);
      //Here is the Message Handling:
      XmlSerializer mser = new XmlSerializer(typeof(Brunet.Chat.Message));
      XmlSerializer pser = new XmlSerializer(typeof(Brunet.Chat.Presence));
      try {
       if( mser.CanDeserialize(r) ) {
        Brunet.Chat.Message mes = (Brunet.Chat.Message)mser.Deserialize(r);
        EventArgs args = new ChatEventArgs(packet.Source, mes);
        Buddy b = GetBuddyWithAddress(packet.Source);
        ChatEvent(b, args);
	byte[] resp = new byte[1];
	man.SendReply(req, resp);
       }
       else if( pser.CanDeserialize(r) ) {
        //This is a presence request, handle it:
        Brunet.Chat.Presence pres = (Brunet.Chat.Presence)pser.Deserialize(r);
        Buddy b = GetBuddyWithAddress(packet.Source);
        if( b != null ) {
          b.Presence = pres; 
          System.IO.MemoryStream ms = new System.IO.MemoryStream();
          XmlWriter w = new XmlTextWriter(ms, System.Text.Encoding.UTF8);
          Brunet.Chat.Presence local_p = new Brunet.Chat.Presence();
          local_p.From = User.Address.ToString();
          local_p.To = b.Address.ToString();
          local_p.Show = User.Show;
          local_p.Status = User.Status;
          pser.Serialize(w, local_p);
          man.SendReply( req, ms.ToArray() ); 
         }
         else {
          //Who is this? as if we are going to reply....
         }
       }
      }
      catch(System.Xml.XmlException x) {
        //We couldn't deserialize...
      }
    }
    /**
     * When the user changes, this code handles it:
     */
    public void UserHandler(object user, System.EventArgs args)
    {
      //Send new status to all buddies:
      foreach(Buddy b in this) {
        b.SendPresence(); 
      }
    }
}


  /**
   * These are the arguments for the event of a chat
   */
  public class ChatEventArgs : System.EventArgs {

    protected Brunet.Chat.Message _message;
    public Brunet.Chat.Message Message {
      get {
        return _message;
      }
    }
    protected Address _source;
    public Address Source { get { return _source; } }

    public ChatEventArgs(Address source, Brunet.Chat.Message m)
    {
      _source = source; 
      _message = m;
    }
  }

}
