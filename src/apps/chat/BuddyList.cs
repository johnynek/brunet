namespace Brunet{
using System;	
using System.IO;
using System.Collections;
using System.Xml.Serialization;
public class BuddyList
{
  private ArrayList buddyArrayList;
  
  public BuddyList()
  {
    buddyArrayList = new ArrayList();
  }

  [XmlArrayItem(ElementName="Buddy",Type=typeof(Buddy))]
  public Buddy[] Buddies {
    get{
      Buddy[] buddies = new Buddy[buddyArrayList.Count];
      buddyArrayList.CopyTo(buddies);
      return buddies;
    }
    set{
      if (null == value ) 
	return;
      Buddy[] buddies = (Buddy[])value;
      buddyArrayList.Clear();
      foreach (Buddy bud in buddies)
	buddyArrayList.Add(bud);
    }
  }
  
  public int Add(Buddy bud){
    return buddyArrayList.Add(bud);
  }
  public void Clear(){
    buddyArrayList.Clear();
  }
}
}
