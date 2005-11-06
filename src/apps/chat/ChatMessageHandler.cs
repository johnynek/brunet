using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Security.Cryptography;
using Gdk;
using Gtk;
namespace Brunet
{
/**
 *
 */
  public class ChatMessageHandler:IRequestHandler
  {
    
    /**
     * Hashtable which maps Buddies -> BrunetChatIM windows
     */
    private Hashtable _message_sinks;
    
    private BrunetChatMain _core_app;

    public Hashtable MessageSinks
    {
      get
      {
        return _message_sinks;
      }
      set
      {
        _message_sinks = value;
      }
    }

    /** Constructor. Passes a mainapp reference.
     */
    public ChatMessageHandler(BrunetChatMain core)
    {
      _core_app = core;
      _message_sinks = new Hashtable();
    }

    /**
     * Close a chat session once we are done with it.
     * @param dest the destination address to close the chat session with
     */
    public void CloseChatSession(Buddy b)
    {
      _message_sinks.Remove(b);
    }
    
    /**
     * Creates a new chat session or raises an existing one in response to a
     * new message.
     * @param recipient Buddy the message is from
     */
    public BrunetChatIM OpenChatSession(Buddy recipient)
    {
      bool doeswindowexist = _message_sinks.Contains( recipient );
      
      BrunetChatIM sink = null;
      if ( !doeswindowexist ) {
         sink = new BrunetChatIM(_core_app.CurrentUser, recipient, this);
         _message_sinks.Add(recipient , sink );
      }
      else {
        sink = (BrunetChatIM)_message_sinks[recipient];
        ///\todo raise window
        ///
      }
      
      return sink;
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
	byte[] resp = HandleMessage(packet, mes);
	man.SendReply(req, resp);
      }
    }

    /**
     * When we get a message, this function handles it
     * @param p the Packet that the message came in?
     * @param message the data sent to us
     * @return the data to send the message sender
     */
    protected byte[] HandleMessage(AHPacket p, Brunet.Chat.Message message) {
      AHAddress sourceaddress = (AHAddress)(p.Source);
       
      bool ismessagefromself = sourceaddress.Equals( _core_app.BrunetNode.Address);
       
      if (true == ismessagefromself)
      {
        Console.WriteLine("Message is from myself.");
        Console.WriteLine("This should never happen.");
        Console.WriteLine("Throw and exception here.");
      }
      else {
        Threads.Enter();
	Buddy b = _core_app.Buddies.GetBuddyWithAddress( sourceaddress );
        BrunetChatIM imwin = OpenChatSession(b);
        imwin.DeliverMessage(message.Body);
	/*
	 * Send a terminal bell when we get a message.
	 * Otherwise, we tend not to notice our Buddies
	 */
	char bell_char = (char)7;
	System.Console.Write(bell_char);
        Threads.Leave();
      }
      //Just let the recipient know we got it
      return new byte[1];
    }
    
  }

}
