namespace Brunet {
using System;
using System.Collections;
using GtkSharp;
using Gtk;
using Gdk;
using Glade;
using System.Security.Cryptography;
using System.Text;
public class BrunetChatMain
{
  /** Button initiates a new chat conversation.
   */
  [Glade.Widget]      
  private Gtk.Button buttonIM;
  
  /** This is a list of Buddies.  It currently does not represent online
   * buddies.  Double-clicking here has the same effect as clicking buttonIM
   */
  [Glade.Widget] 
  private Gtk.TreeView treeviewBuddies;
  
  /** The Main window widget.
   */
  [Glade.Widget]      
  private Gtk.Window windowBrunetChatMain;
  
  /** Column where buddy alias is displayed.
   */
  private TreeViewColumn _buddy_col;
  
  /** Column where buddy email is displayed.
   */
  private TreeViewColumn _email_col;
  
  /** This is the user of the chat program. In the future one will be able to
   * log out and log in as a different user.
   */
  private User _current_user;
  
  /** The AHAddress of the chat node.
   */
  private AHAddress _local_ahaddress;
  
  /** internal storage for the tree view.
   */
  private ListStore _store;
  
  /** Serialization of configuration data.
   */
  private ChatConfigSerialization _chat_config;
  
  /** Handles incoming chat messages and routes them to the correct ChatIM
   * window.
   */
  private ChatMessageHandler _message_handler;
  
  /** email -> Buddy
   */
  private Hashtable _buddy_hash; 

  /**
   * Allows us to use the request/reply protocol for chats
   */
  protected ReqrepManager _rrman;
  public ReqrepManager RRMan { get { return _rrman; } }
  
  /** the brunet node of the chat client.
   */
  private StructuredNode _brunet_node;
  
  public AHAddress LocalAhAddress
  {
    set{
      _local_ahaddress = value;
    }
    get{
      return _local_ahaddress;
    }
  }
  
  public User CurrentUser
  {
    get
    {
      return _current_user;
    }
  }
  
  public StructuredNode BrunetNode
  {
    get
    {
      return _brunet_node;
    }
  }

  public ChatMessageHandler MessageHandler
  {
    get
    {
      return _message_handler;
    }
  }
  
  public Hashtable BuddyHash
  {
    get
    {
      return _buddy_hash;
    }
    set
    {
      _buddy_hash = value;
    }
  }
 
  /** Main program. Logs in the user and creates the main window.
   */
  public static void Main (string[] args)
  {
    if (args.Length < 0){
      Console.WriteLine ("Usage:");
      return;
    }
    
    Threads.Init();
    Application.Init();
    
    ChatConfigSerialization chat_config = 
      new ChatConfigSerialization("Users.xml");
    
    chat_config.DeserializeUserList();  
    BrunetChatLogin dialog = new BrunetChatLogin(chat_config);
     
    dialog.dialogBrunetChatLogin.Run();
    
    if( false == dialog.userStrings.Contains(dialog.ComboEmail) )
    {
      chat_config.UserList.Prepend(new User("alias",dialog.ComboEmail,true) );
      chat_config.SerializeUserList();
    }
    
    BrunetChatMain app = new BrunetChatMain(dialog.CurrentUser(),chat_config);
    
    dialog.dialogBrunetChatLogin.Hide();
  
    Application.Run();
    app._store = null;
  }
    
  /** Constructor. 
   * @param a_user The local user who will being chatting
   * @param chat_config Serializable meta-data 
   */
  public BrunetChatMain(User a_user ,ChatConfigSerialization chat_config)
  {
    string fname = "BrunetChat.glade";
    string root = "windowBrunetChatMain";
    Glade.XML gxml = new Glade.XML (fname, root, null);
    //Glade.XML gxml = new Glade.XML (null,fname, root, null);
    gxml.Autoconnect(this);
    
    //compute the sha1 hash of the email address(UTF-8 encoded) and
    //use that the hash as the 160-bit address.
    _current_user = a_user;
    SHA1 sha = new SHA1CryptoServiceProvider(); 
    byte[] hashedemail = sha.ComputeHash(Encoding.UTF8.GetBytes(_current_user.Email));
    //inforce type 0
    hashedemail[Address.MemSize - 1] &= 0xFE;
    _local_ahaddress = new AHAddress(hashedemail);
    _brunet_node = new StructuredNode(_local_ahaddress);

    _chat_config = chat_config;
    _chat_config.BuddyListFilename = "Buddylist.xml";
    _chat_config.RemoteTAsFilename = "RemoteTransportAddresses.xml";
    _chat_config.LocalTcpPortFilename = "LocalTcpPort.xml";
    
    _chat_config.DeserializeLocalTcpPort();
    _brunet_node.AddEdgeListener( 
        new TcpEdgeListener(_chat_config.LocalTcpPort.TcpPort) );
    
    treeviewBuddies = (TreeView)gxml["treeviewBuddies"];
    _store = new ListStore(typeof(string),typeof(string));
    treeviewBuddies.Model = _store;

    _buddy_col = new TreeViewColumn ();
    CellRenderer buddyrenderer = new CellRendererText ();
    _buddy_col.Title = "Buddy";
    _buddy_col.PackStart (buddyrenderer, true);
    _buddy_col.AddAttribute (buddyrenderer, "text", 0);
    treeviewBuddies.AppendColumn (_buddy_col);
    
    _email_col = new TreeViewColumn ();
    CellRenderer emailrenderer = new CellRendererText ();
    _email_col.Title = "Email";
    _email_col.PackStart (emailrenderer, true);
    _email_col.AddAttribute (emailrenderer, "text", 1);
    treeviewBuddies.AppendColumn (_email_col);
    
    _chat_config.DeserializeBuddyList();
    _buddy_hash = new Hashtable();
    foreach (Buddy bud in _chat_config.BuddyList.Buddies){
      byte[] budhashedemail = sha.ComputeHash(
          Encoding.UTF8.GetBytes(bud.Email));
      //inforce type 0
      budhashedemail[Address.MemSize - 1] &= 0xFE;
      AHAddress ahaddress = new AHAddress(budhashedemail);
      _buddy_hash.Add(ahaddress,bud);
      _store.AppendValues(bud.Alias, bud.Email);
    }
    
    _chat_config.DeserializeRemoteTAs();
    string[] tas = _chat_config.RemoteTAs.TAs;
    foreach (string ta in tas)
    {
      _brunet_node.RemoteTAs.Add(new TransportAddress(ta) );
      Console.WriteLine(ta);
    }
    _message_handler = new ChatMessageHandler(this);
    //_brunet_node.Subscribe(AHPacket.Protocol.Chat,_message_handler);
    _rrman = new ReqrepManager(_brunet_node);
    _rrman.Bind( AHPacket.Protocol.Chat, _message_handler);
    _brunet_node.Connect();
  }

  public void OnButtonAddBuddyClicked(System.Object obj, EventArgs args) 
  {
    BrunetChatAddBuddy dialog = new BrunetChatAddBuddy();
    dialog.dialogBrunetChatAddBuddy.Run();
    Buddy newbud = dialog.NewBuddy;
    if (newbud != null){
      SHA1 sha = new SHA1CryptoServiceProvider();  
      byte[] budhashedemail = sha.ComputeHash(Encoding.UTF8.GetBytes(newbud.Email));
      //inforce type 0
      budhashedemail[Address.MemSize - 1] &= 0xFE;
      AHAddress ahaddress = new AHAddress(budhashedemail);
      /// check that the new buddy is not already in the buddy hashtable
      if (! _buddy_hash.Contains(ahaddress) ){
        _buddy_hash.Add(ahaddress,newbud);
        _chat_config.BuddyList.Add(newbud);
        _store.AppendValues(newbud.Alias, newbud.Email);
      }
    }
    
    dialog.dialogBrunetChatAddBuddy.Destroy(); 
  }

  
  public void OnButtonImClicked(System.Object obj, EventArgs args) 
  {
    TreeSelection row_sel = treeviewBuddies.Selection;
    TreeIter iter;
    TreeModel model;
    if ( row_sel.GetSelected (out model, out iter) ){
      string val = (string) model.GetValue (iter, 1);
      _message_handler.OpenChatSession(val);
    }
  }

  public void OnWindowBrunetChatMainDeleteEvent (object o, DeleteEventArgs args) 
	{
		args.RetVal = true;
    
    ///\todo harvest new TAs from the node
    _chat_config.SerializeRemoteTAs();
    
    // write updated buddylist to file
    _chat_config.SerializeBuddyList();

    windowBrunetChatMain.Hide();
    Console.WriteLine("quit");
    _brunet_node.Disconnect();
    Application.Quit();
	}

  //Double clicks on the tree view
  public void on_treeviewBuddies_row_activated (object o, EventArgs args)
  {

    TreeSelection row_sel = ((TreeView)o).Selection;
    TreeIter iter;
    TreeModel model;
 
    try{
      if ( row_sel.GetSelected (out model, out iter) ){
        string val = (string) model.GetValue (iter, 1);
        _message_handler.OpenChatSession(val);
      }
    }  
    catch(Exception ex){
	    Console.WriteLine(ex.Message);
	  }
  }
}
}
