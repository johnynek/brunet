namespace Brunet{
using System;	
using System.IO;
using System.Collections;
using System.Xml.Serialization;
public class UserList
{
  private ArrayList userArrayList;
  
  public UserList()
  {
    userArrayList = new ArrayList();
  }

  [XmlArrayItem(ElementName="User",Type=typeof(User))]
  public User[] Users {
    get{
      User[] users = new User[userArrayList.Count];
      userArrayList.CopyTo(users);
      return users;
    }
    set{
      if (null == value ) 
	return;
      User[] users = (User[])value;
      userArrayList.Clear();
      foreach (User bud in users)
	userArrayList.Add(bud);
    }
  }
  
  public int Add(User bud){
    return userArrayList.Add(bud);
  }
  public void Prepend(User bud){
    userArrayList.Insert(0,bud);
  }
  public void Clear(){
    userArrayList.Clear();
  }
}
}
