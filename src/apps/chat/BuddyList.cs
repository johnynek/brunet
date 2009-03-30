using System;	
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;

namespace Brunet{

[XmlRootAttribute("BuddyList")]
public class BuddyList : Brunet.IRpcHandler
{
  private ArrayList buddyArrayList;
 
  protected readonly WriteOnce<Node> _node;
  [XmlIgnore]
  public Node Node {
    get { return _node.Value; }
    set {
      _node.Value = value;
      value.Rpc.AddHandler("example:chat", this);
    }
  }
  
  public BuddyList()
  {
    _node = new WriteOnce<Node>();
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
	Console.WriteLine("Address: {0} Email: {1}", bud.Sender.ToUri(), bud.Email);
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
    if( !Contains(bud) ) {
      _add_to_buddy[ bud.Sender.ToUri() ] = bud;
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
    if( b.Sender == null ) { return false; }
    return _add_to_buddy.ContainsKey(b.Sender.ToUri());
  }
  /**
   * @return the buddy in the list that has the given Address
   */
  public Buddy GetBuddyWithSender(ISender a)
  {
    return (Buddy)_add_to_buddy[a.ToUri()];
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
 
  public void HandleRpc(ISender caller, string method, IList arguments, object request_state) {
    if( method == "message") {
      var sr = new System.IO.StringReader((string)arguments[0]);
      XmlReader r = new XmlTextReader(sr);
      var mser = new XmlSerializer(typeof(Brunet.Chat.Message));
      var message = (Brunet.Chat.Message)mser.Deserialize(r);
      var args = new ChatEventArgs(caller, message);
      Buddy b = GetBuddyWithSender(caller);
      ChatEvent(b, args);
      _node.Value.Rpc.SendResult(request_state, true);
    }
    else if( method == "presence" ) {
      Buddy b = GetBuddyWithSender(caller);
      if (b == null) { throw new Exception("Unknown buddy: " + caller.ToUri()); }
      //Read the presence information:
      var sr = new System.IO.StringReader((string)arguments[0]);
      XmlReader r = new XmlTextReader(sr);
      var pser = new XmlSerializer(typeof(Brunet.Chat.Presence));
      Brunet.Chat.Presence pres = (Brunet.Chat.Presence)pser.Deserialize(r);
      b.Presence = pres; 
      //Send our response:
      System.IO.StringWriter sw = new System.IO.StringWriter();
      var tw = new XmlTextWriter(sw);
      Brunet.Chat.Presence local_p = new Brunet.Chat.Presence();
      local_p.Show = User.Show;
      local_p.Status = User.Status;
      pser.Serialize(tw, local_p);
      _node.Value.Rpc.SendResult(request_state, sw.ToString());
    }
    else {
      throw new Exception("Unknown method: " + method);
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
    protected ISender _source;
    public ISender Source { get { return _source; } }

    public ChatEventArgs(ISender source, Brunet.Chat.Message m)
    {
      _source = source; 
      _message = m;
    }
  }

}
