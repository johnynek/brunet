using System.Xml.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Brunet{

/**
 * Represents a Buddy in the Chat client
 */
public class Buddy
{
  private string alias;
  private string email;
  
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

  protected string _status;
  /*
   * The status of the given Buddy
   */
  public string Status {
    get {
      return _status;
    }
  }
  /*
   * The Brunet Address for this Buddy
   */
  protected Address _add;
  public Address Address {
    get {
      if( null == _add && Email != "") { 
        SHA1 sha = new SHA1CryptoServiceProvider();  
        byte[] budhashedemail = sha.ComputeHash(Encoding.UTF8.GetBytes(Email));
        //inforce type 0
        budhashedemail[Address.MemSize - 1] &= 0xFE;
        _add = new AHAddress(budhashedemail);
      }
      return _add;
    }
  }
  
  public Buddy(){
    _status = "online";
  }
  
  public Buddy(string a_alias,string a_email)
  {
    Alias = a_alias;
    Email = a_email;
    _status = "online";
  }


}

}
