using System;
using System.Collections;
using System.Runtime.CompilerServices;
using Gdk;
namespace Brunet 
{

   // A class that works just like Queue, but sends event
   // notifications whenever a new object is enqueued
   // Also there is an AHAddress object so that there is one event type for
   // each chat session.
   
   public class ImQueue: Queue
   {
      public delegate void EnqueueHandler(object message);
      protected AHAddress senderAddress;
      private DelegateHashtable delegates = new DelegateHashtable();
      
      public ImQueue(AHAddress address):base()
      {
        senderAddress = address;
      }
      
      // An event that clients can use to be notified whenever the
      // a new object is enqueued
      public event EnqueueHandler Enqueued
      {
        [MethodImpl(MethodImplOptions.Synchronized)]
        add
        {
          delegates.Add(senderAddress, value);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        remove
        {
          delegates.Remove(senderAddress, value);
        }
      }

      // Invoke the Enqueued event
      protected virtual void OnEnqueued(object message) 
      {
        EnqueueHandler eh = (EnqueueHandler) delegates.Find(senderAddress);

        if (eh != null)
          eh( message );
      }

      public override void Enqueue(object value) 
      {
         base.Enqueue(value);
         OnEnqueued( Dequeue() );
      }
   }
}

