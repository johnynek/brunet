using System;
using System.Runtime.CompilerServices;
using System.Collections;

namespace Brunet
{

public class DelegateHashtable
{
   public Hashtable delegateStorage = new Hashtable();
   public Delegate Find(AHAddress key)
   {
      return((Delegate) delegateStorage[key]);      
   }
   public void Add(AHAddress key, Delegate myDelegate)
   {
      delegateStorage[key] = 
         Delegate.Combine((Delegate) delegateStorage[key], 
                myDelegate);
   }
   public void Remove(AHAddress key, Delegate myDelegate)
   {
      delegateStorage[key] = 
         Delegate.Remove((Delegate) delegateStorage[key], 
                myDelegate);
   }
}
}
