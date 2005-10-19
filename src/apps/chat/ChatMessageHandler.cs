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
    public void CloseChatSession(AHAddress dest)
    {
      _message_sinks.Remove(dest);
    }
    
    /** Takes an email address and converts to AHAddress
     * @param remoteemailaddress email of the recipient
     */
    public BrunetChatIM OpenChatSession(string remoteemailaddress)
    {
      SHA1 sha = new SHA1CryptoServiceProvider(); 
      byte[] hashedemail = sha.ComputeHash(Encoding.UTF8.GetBytes(remoteemailaddress));
      //inforce type 0
      hashedemail[Address.MemSize - 1] &= 0xFE;
      AHAddress remoteahaddress = new AHAddress(hashedemail);
      return OpenChatSession(remoteahaddress);
    }
    
    /** Creates a new chat session or raises an existing one in response to a
     * new message.
     * @param remoteahaddress AHAddress of the recipient
     */
    public BrunetChatIM OpenChatSession(AHAddress remoteahaddress)
    {
      bool doeswindowexist =  _message_sinks.Contains( remoteahaddress );
      
      BrunetChatIM sink;
      if ( !doeswindowexist  ){
         sink = new BrunetChatIM(_core_app,remoteahaddress);
         _message_sinks.Add(sink.ToAddress , sink );
      }
      else{
        sink = (BrunetChatIM)_message_sinks[remoteahaddress];
        ///\todo raise window
        ///
      }
      
      return sink;
    }
    
    /*
     * This is a request of us.
     */
    public void HandleRequest(ReqrepManager man, ReqrepManager.ReqrepType rt,
		              object req, string prot, System.IO.MemoryStream payload, AHPacket packet)
    {
      Console.WriteLine("Got request");
      string msg = Encoding.UTF8.GetString( payload.ToArray() );
      AHAddress sourceaddress = (AHAddress)(packet.Source);
       
      bool ismessagefromself = sourceaddress.Equals( _core_app.BrunetNode.Address);
       
      if (true == ismessagefromself)
      {
        Console.WriteLine("Message is from myself.");
        Console.WriteLine("This should never happen.");
        Console.WriteLine("Throw and exception here.");
      }
      else {
        Console.WriteLine("Got: {0}.", msg);
        Threads.Enter();
        BrunetChatIM imwin = OpenChatSession( sourceaddress);
        imwin.DeliverMessage(msg);
	/*
	 * Send a terminal bell when we get a message.
	 * Otherwise, we tend not to notice our Buddies
	 */
	char bell_char = (char)7;
	System.Console.Write(bell_char);
        Threads.Leave();
      }
      //Now send the reply
      man.SendReply(req, new byte[1]);
    }
    
  }

}
