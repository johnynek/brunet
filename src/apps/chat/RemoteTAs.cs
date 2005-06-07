
using System.Collections;
using System;	
using System.IO;
using System.Xml.Serialization;

namespace Brunet{

public class RemoteTAs
{
  private ArrayList remoteTAList;
  
  public RemoteTAs()
  {
    remoteTAList = new ArrayList();
  }

  [XmlArrayItem(ElementName="TransportAddress",Type=typeof(string))]
  public string[] TAs {
    get{
      string[] tas = new string[remoteTAList.Count];
      remoteTAList.CopyTo(tas);
      return tas;
    }
    set{
      if (null == value ) 
	      return;
      remoteTAList.Clear();
      foreach (string ta in value)
	      remoteTAList.Add( ta );
    }
  }
  
  public int AddTA(string ta){
    return remoteTAList.Add(ta);
  }
  
  public void ClearTAs(){
    remoteTAList.Clear();
  }

  /**
   * This method can be used to set the TAs from an enumerable list
   * of TransportAddress objects
   */
  public void SetTAs(IEnumerable tas)
  {
    remoteTAList.Clear();
    foreach(TransportAddress ta in tas)
    {
      remoteTAList.Add( ta.ToString() );
    }
  }
	  
}
}
