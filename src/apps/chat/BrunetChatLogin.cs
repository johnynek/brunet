namespace Brunet {
using System;
using GtkSharp;
using Gtk;
using Glade;
using System.Configuration;
using System.Collections;
using System.Text;

public class BrunetChatLogin 
{
  
  [Glade.Widget]      
  public Gtk.Button okButton;
  [Glade.Widget]      
  public Gtk.Button cancelButton;
  [Glade.Widget]      
  public Gtk.Entry entryEmail;

  [Glade.Widget]
  public Gtk.Combo comboEmail;
  [Glade.Widget]      
  public Gtk.Dialog dialogBrunetChatLogin;
    
  private string _sender_email;
  private ChatConfigSerialization _chat_config;
  public ArrayList userStrings;
  public Hashtable userHashtable;
    
  public BrunetChatLogin(ChatConfigSerialization config)
  {
    _chat_config = config;
    
    string fname = "BrunetChat.glade";
    string root = "dialogBrunetChatLogin";
    Glade.XML gxml = new Glade.XML (fname, root, null);
    //Glade.XML gxml = new Glade.XML (null,fname, root, null);
    gxml.Autoconnect(this);

    int init_users = _chat_config.UserList.Users.Length;
    if (0 < init_users){
      userStrings = new ArrayList();
      userHashtable = new Hashtable();
      foreach(User user in _chat_config.UserList.Users ){
        userHashtable.Add(user.Email,user); 
        userStrings.Add(user.Email);
      }
      
      int count =  userStrings.Count;
      Console.WriteLine("User count: {0}",count);
      string[] popstring = new string[count];
      for (int loop = 0;loop<count;loop++){
        popstring[loop] = (string)userStrings[loop];
        Console.WriteLine(popstring[loop]);
      }
      comboEmail.PopdownStrings = popstring;
    }
    
  }

  public string ComboEmail
  {
    get
    {
      return comboEmail.Entry.Text;
    }
  }

  public User CurrentUser()
  {
    if (userHashtable.Contains(ComboEmail))
      return (User)userHashtable[ComboEmail];
    else
      return null;
  }
  
  public string SenderEmail
  {
    get
    {
      return _sender_email;
    }
    set
    {
      if (null != value )
        _sender_email = value;
    }
  }
  
  public void  OnButtonCancelClicked(object obj, EventArgs e) 
  { 
    Application.Quit ();
  }
  
}

}
