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
      XmlReader r = new XmlTextReader(payload);
      //Here is the Message Handling:
      XmlSerializer mser = new XmlSerializer(typeof(Brunet.Chat.Message));
      if( mser.CanDeserialize(r) ) {
        Brunet.Chat.Message mes = (Brunet.Chat.Message)mser.Deserialize(r);
        EventArgs args = new ChatEventArgs(packet.Source, mes);
        Buddy b = GetBuddyWithAddress(packet.Source);
        ChatEvent(b, args);
	byte[] resp = new byte[1];
	man.SendReply(req, resp);
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
