using System.Security.Cryptography;
using System.Xml.Serialization;
using System.Text;

namespace Brunet{

public class User
{
  private string alias;
  private string email;
  private bool isdefault;
 
  protected AHAddress _add;
  public AHAddress Address {
    get {
      if( null == _add && Email != "") { 
        SHA1 sha = new SHA1CryptoServiceProvider();  
        byte[] budhashedemail = sha.ComputeHash(Encoding.UTF8.GetBytes(Email));
        //inforce type 0
        budhashedemail[Brunet.Address.MemSize - 1] &= 0xFE;
        _add = new AHAddress(budhashedemail);
      }
      return _add;
    }
  }
  
  [XmlAttribute("Alias")] 
  public string Alias{
    get{
      return alias;
    }
    set{
      if(null!=value)
        alias = value;
    }
  }	  
  
  [XmlAttribute("Email")] 
  public string Email{
    get{
      return email;
    }
    set{
      if(null!=value)
        email = value;
    }
  } 
  
  [XmlAttribute("IsDefault")] 
  public bool IsDefault{
    get{
      return isdefault;
    }
    set{
        isdefault = value;
    }
  } 
  
  protected string _show;
 
  [XmlIgnore]
  /**
   * What is shown to our buddies (chat, away, dnd)
   * @see Presence.Show
   */
  public string Show {
    get { return _show; }
    set {
      if( _show != value ) {
        _show = value;
	if( Changed != null ) {
          Changed(this, System.EventArgs.Empty);
	}
      }
    }
  }
  protected string _status;
  [XmlIgnore]
  /**
   * This is the string we set which describes what we are doing
   * (e.g. show: away, status: sleeping)
   */
  public string Status {
    get { return _status; }
    set {
      if( _status != value ) {
        _status = value;
	if( Changed != null ) {
          Changed(this, System.EventArgs.Empty);
	}
      }
    }
  }

  /*
   * When something about the user changes this event
   * is fired
   */
  public event System.EventHandler Changed;
  
  public User(){
  }
  
  public User(string a_alias,string a_email,bool a_isdefault)
  {
    Alias = a_alias;
    Email = a_email;
    IsDefault = a_isdefault;
  }


}

}
