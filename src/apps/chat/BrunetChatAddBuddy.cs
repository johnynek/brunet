namespace Brunet {
using System;
using GtkSharp;
using Gtk;
using Glade;
using System.Configuration;
public class BrunetChatAddBuddy 
{
  [Glade.Widget]      
  public Gtk.Button okButton;
  [Glade.Widget]      
  public Gtk.Entry entryEmail;
  [Glade.Widget]      
  public Gtk.Entry entryAlias;
  [Glade.Widget]      
  public Gtk.Dialog dialogBrunetChatAddBuddy;
    
  public BrunetChatAddBuddy()
  {
    string fname = "BrunetChat.glade";
    string root = "dialogBrunetChatAddBuddy";
    Glade.XML gxml = new Glade.XML (fname, root, null);
    //Glade.XML gxml = new Glade.XML (null,fname, root, null);
    gxml.Autoconnect(this);
  }

  public Buddy NewBuddy{
    get{
      if (entryEmail.Text != null)
        return new Buddy(entryAlias.Text,entryEmail.Text);
      else
        return null;
    }
  } 
}
}
