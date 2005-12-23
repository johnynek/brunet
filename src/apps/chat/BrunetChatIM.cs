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
public class BrunetChatIM 
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
 
  /**
   * Reference to the BrunetChatMain.  We need
   * to tell this object when we close the chat
   * message
   */
  private BrunetChatMain _bcm;
  
  /** The buddy who will recieve messages.
   */
  private Buddy _recipient_buddy;
  public Buddy ToBuddy {
    get { return _recipient_buddy; }
  }
  
  /**
   * This is the user who is sending the chats
   */
  private User _user;
  
  /** ChatIM constructor. 
   *  @param core the main application
   *  @param r_add the recipient address
   */
  public BrunetChatIM(User u, Buddy bud, BrunetChatMain bcm)
  {
    _user = u;
    _recipient_buddy = bud;
    _bcm = bcm;
    
    string fname = "BrunetChat.glade";
    string root = "windowBrunetChatIM";

    Glade.XML gxml = new Glade.XML (fname, root, null);
    //Glade.XML gxml = new Glade.XML (null,fname, root, null);
    gxml.Autoconnect(this);
    
    _text_buf_display = textviewDisplay.Buffer;
    _text_buf_input = textviewInput.Buffer;
    _text_buf_recipient = textviewRecipient.Buffer;
    _text_buf_recipient.Text = _recipient_buddy.Alias;
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
          _recipient_buddy.SendMessageText( _text_buf_input.Text );
          string sender_preamble = "<"+ _user.Alias +"> ";
          TextTag[] sender_ar;
          sender_ar = new TextTag[1];
          TextTag t_tag = _text_buf_display.TagTable.Lookup("Sender Color");
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
	  _text_buf_display.Insert(_text_buf_display.EndIter, _text_buf_input.Text);
          _text_buf_display.Insert(_text_buf_display.EndIter, System.Environment.NewLine);
      
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

  public void OnWindowDeleteEvent (object o, DeleteEventArgs args) 
  {
    args.RetVal = true;
    windowBrunetChatIM.Destroy();
    _bcm.CloseChatSession(_recipient_buddy);
  }
  
}

}
