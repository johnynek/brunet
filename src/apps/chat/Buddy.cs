namespace Brunet{
using System.Xml.Serialization;
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
  
  public Buddy(){
  }
  
  public Buddy(string a_alias,string a_email)
  {
    Alias = a_alias;
    Email = a_email;
  }


}

}
