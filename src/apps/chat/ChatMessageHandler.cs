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
  public class ChatMessageHandler:IAHPacketHandler
  {
    
    private Hashtable _sender_to_queue;
    
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
      _sender_to_queue = new Hashtable();
      _message_sinks = new Hashtable();
    }

    /** Takes an email address and converts to AHAddress
     * @param remoteemailaddress email of the recipient
     */
    public void OpenChatSession(string remoteemailaddress)
    {
      SHA1 sha = new SHA1CryptoServiceProvider(); 
      byte[] hashedemail = sha.ComputeHash(Encoding.UTF8.GetBytes(remoteemailaddress));
      //inforce type 0
      hashedemail[Address.MemSize - 1] &= 0xFE;
      AHAddress remoteahaddress = new AHAddress(hashedemail);
      OpenChatSession(remoteahaddress);
    }
    
    /** Creates a new chat session or raises an existing one in response to a
     * new message.
     * @param remoteahaddress AHAddress of the recipient
     */
    public void OpenChatSession(AHAddress remoteahaddress)
    {
      bool doeswindowexist =  _message_sinks.Contains( remoteahaddress );
      bool doesqueueexist =  _sender_to_queue.Contains( remoteahaddress );
      
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
      
      if ( !doesqueueexist ){
        ImQueue newqueue = new ImQueue(remoteahaddress);
        _sender_to_queue.Add(remoteahaddress,newqueue);
        newqueue.Enqueued += new ImQueue.EnqueueHandler(sink.DeliverMessage);
      }
    }
    
    
    public void HandleAHPacket(object node, AHPacket p, Edge from)
    {
      //extract text from packet
      MemoryStream mems = p.PayloadStream;
      string msg = Encoding.UTF8.GetString( mems.ToArray() );
      AHAddressComparer cmp = new AHAddressComparer();
      AHAddress sourceaddress = (AHAddress)(p.Source);
       
      bool ismessagefromself = ( 0 == cmp.Compare( 
            sourceaddress ,
            (AHAddress)_core_app.BrunetNode.Address)  );
       
      if (true == ismessagefromself)
      {
        Console.WriteLine("Message is from myself.");
        Console.WriteLine("This should never happen.");
        Console.WriteLine("Throw and exception here.");
      }
      else {
        OpenChatSession( sourceaddress);
        ImQueue tmpqueue = (ImQueue)_sender_to_queue[sourceaddress];
        tmpqueue.Enqueue(msg);
      }
    }
    
    public bool HandlesAHProtocol(AHPacket.Protocol type)
    {
      return (type == AHPacket.Protocol.Chat);
    }
  }

}
