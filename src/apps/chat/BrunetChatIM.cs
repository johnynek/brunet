namespace Brunet 
{
using System;
using GtkSharp;
using Gtk;
using Gdk;
using Glade;
using System.Configuration;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Security.Cryptography;
public class BrunetChatIM : IReplyHandler
{
  /** The chat window widget. There is precisely one ChatIM for each
   * conversation.
   */
  [Glade.Widget]      
  private Gtk.Window windowBrunetChatIM; 
  
  /** The message send widget. This widget is the default action in the ChatIM
   * window...that is <enter> triggers the same handler as a click. 
   */
  [Glade.Widget]      
  private Gtk.Button buttonSend;

  /** The conversation is displayed here.
   */
  [Glade.Widget]      
  private Gtk.TextView textviewDisplay;

  /** The outgoing text is entered here.
   */
  [Glade.Widget]      
  private Gtk.TextView textviewInput;

  /** The recipient of messages initiated in this ChatIM is displayed here.
   */
  [Glade.Widget]      
  private Gtk.TextView textviewRecipient;

  /// Buffers for the above TextView widgets.
  ///
 
  private Gtk.TextBuffer _text_buf_display;
  private Gtk.TextBuffer _text_buf_input;
  private Gtk.TextBuffer _text_buf_recipient;
 
  /** The BrunetNode for this chat program instance. This quantity is a
   * reference to the node in BrunetChatMain.cs.
   */
  private StructuredNode _brunet_node;

  /** A reference to the main chat window application object.
   */
  private BrunetChatMain _brunet_chat_main;
  
  /** The buddy who will recieve messages.
   */
  private Buddy _recipient_buddy;
  
  /** The Brunet address of the recipient.
   */
  private AHAddress _to_address;

  /** The Brunet address of the sender.
   */
  private AHAddress _from_address;
  
  /** This string is prepended before each outgoing message.
   */
  private string _sender_alias;
  
  public AHAddress ToAddress
  {
    get
    {
      return _to_address;
    }
  }
  
  /** ChatIM constructor. 
   *  @param core the main application
   *  @param r_add the recipient address
   */
  public BrunetChatIM(BrunetChatMain core,AHAddress r_add)
  {
    _brunet_chat_main = core;
    _brunet_node = _brunet_chat_main.BrunetNode;
    _from_address = (AHAddress)_brunet_node.Address; 
    _to_address = r_add;
    
    string fname = "BrunetChat.glade";
    string root = "windowBrunetChatIM";

    Glade.XML gxml = new Glade.XML (fname, root, null);
    //Glade.XML gxml = new Glade.XML (null,fname, root, null);
    gxml.Autoconnect(this);
    
    _text_buf_display = textviewDisplay.Buffer;
    _text_buf_input = textviewInput.Buffer;
    _text_buf_recipient = textviewRecipient.Buffer;
    _recipient_buddy = (Buddy)_brunet_chat_main.BuddyHash[_to_address]; 
    _text_buf_recipient.Text = _recipient_buddy.Alias;
    _sender_alias = (string)_brunet_chat_main.CurrentUser.Alias;
    Gdk.Color red_color = new Gdk.Color (0xff, 0, 0);
    Gdk.Color blue_color = new Gdk.Color (0, 0, 0xff);
    
    TextTag sendercolor = new TextTag("Sender Color");
    sendercolor.ForegroundGdk = blue_color;
    
    TextTag recipientcolor = new TextTag("Recipient Color");
    recipientcolor.ForegroundGdk = red_color;
    _text_buf_display.TagTable.Add(sendercolor);
    _text_buf_display.TagTable.Add(recipientcolor);
    
  }

  /** Button click handler.  This sends input text to the node for delivery
   * and also echoes the text into the display window.
   */
  public void  OnButtonSendClicked(object obj, EventArgs e) 
  {
    if (null != obj){
      if (_text_buf_input != null){
        if (_text_buf_input.CharCount > 0 ){
          SendText(_text_buf_input.Text);
          string sender_preamble = "<"+ _sender_alias +"> ";
          TextTag[] sender_ar;
          sender_ar = new TextTag[1];
          TextTag t_tag = _text_buf_display.TagTable.Lookup("Sender Color");
          //Console.WriteLine(t_tag.ForegroundGdk.ToString());
          if (t_tag != null)
          {
            sender_ar[0] = t_tag;
            _text_buf_display.InsertWithTags(
              _text_buf_display.GetIterAtMark(_text_buf_display.InsertMark),
              sender_preamble,
              sender_ar
              );
          }
          else
          {
            Console.WriteLine("Error: Back Text Tag In TextBuffer");
          }
          _text_buf_display.Text += _text_buf_input.Text;
          _text_buf_display.Text += "\n";
          _text_buf_display.MoveMark(
              _text_buf_display.InsertMark, 
              _text_buf_display.EndIter);
          textviewDisplay.ScrollToMark(
              _text_buf_display.InsertMark, 
              0.4,
              true, 
              0.0, 
              1.0);
          _text_buf_input.Clear();
        }  
      }
    }
    else
    {
      Console.WriteLine("Gtk error null reference");
      throw new NullReferenceException();
    }
  }

  /** Packetize the text as UTF8 and send it.  Eventually we will want to use
   * Jabber or some other standard meesage format.
   * @param sendtext This string will be packetized and sent to the recipient.
   */
  protected void SendText(string sendtext)
  {
    Console.WriteLine("sending {0}",sendtext);
    byte[] payload = Encoding.UTF8.GetBytes(sendtext);
    int req_num = _brunet_chat_main.RRMan.SendRequest( _to_address,
                                        ReqrepManager.ReqrepType.Request,
                                        AHPacket.Protocol.Chat,
                                        payload,
                                        this,
                                        null);
  }
 
  /** This is called when new text arrives from the recipient.
   * Text is inserted into the display, the display is scrolled if needed and
   * the message is written to the console for debugging.
   */
  public void DeliverMessage(object ob)
  {
    if (null != ob){
      string a_msg = (string)ob;

      string recipient_preamble = "<"+_recipient_buddy.Alias+"> ";
      TextTag[] recipient_ar;
      recipient_ar = new TextTag[1];
      TextTag t_tag = _text_buf_display.TagTable.Lookup("Recipient Color");
      Console.WriteLine(t_tag.ForegroundGdk.ToString());
      if (t_tag != null)
      {
        recipient_ar[0] = t_tag;
        _text_buf_display.InsertWithTags(
            _text_buf_display.GetIterAtMark(_text_buf_display.InsertMark),
            recipient_preamble,
            recipient_ar  
            );    
      }
      else
      {
        Console.WriteLine("Error: Back Text Tag In TextBuffer");
      }

      //_text_buf_display.Insert(
      //    _text_buf_display.EndIter,
      //    "<"+_recipient_buddy.Alias+"> " );
      
      Console.WriteLine(a_msg ); 
      
      _text_buf_display.Insert(_text_buf_display.EndIter,a_msg);
      _text_buf_display.Insert(
          _text_buf_display.EndIter,
          System.Environment.NewLine);
      
      _text_buf_display.MoveMark(
          _text_buf_display.InsertMark, 
          _text_buf_display.EndIter);
      textviewDisplay.ScrollToMark(
          _text_buf_display.InsertMark, 
          0.4,
          true, 
          0.0, 
          1.0);
    }
    else
      Console.WriteLine("Message is NULL" ); 
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

  public void OnWindowDeleteEvent (object o, DeleteEventArgs args) 
  {
    args.RetVal = true;
    windowBrunetChatIM.Destroy();
    _brunet_chat_main.MessageHandler.CloseChatSession(_to_address);
  }
  
}

}
