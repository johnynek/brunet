namespace Brunet{
using System.Xml.Serialization;
public class User
{
  private string alias;
  private string email;
  private bool isdefault;
  
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
