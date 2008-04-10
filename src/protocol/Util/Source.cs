/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, University of Florida
Copyright (C) 2008 David Wolinsky <davidiw@ufl.edu>, University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using System;
using System.Collections;

namespace Brunet {
  /**
  * This is a source of data
  */
  public interface ISource {
    /**
    <summary>Subscribes the out going data from this source to the specified
    handler.</summary>
    <param name="hand">Data that the subscriber wants passed to the handler on
    each call.</param>
    <param name="state">Data that the subscriber wants passed to the handler on
    each call.</param>
    */
    void Subscribe(IDataHandler h, object state);

    /**
    <summary>Unsubscribes the the IDataHandler if it is the current
    IDatahandler.</summary>
    <param name="hand">An IDataHandler that wishes to remove itself as a
    destination for data coming from this source.</param>
    */
    void Unsubscribe(IDataHandler h);
  }


  /// <summary>This holds a single subscriber for an ISource.<summary>
  public class Subscriber {
    /// <summary>The handler of the subscriber who will receive data.</summary>
    public readonly IDataHandler Handler;
    /**  <summary>Data that the subscriber wants passed to the handler on each
    call.</summary> */
    public readonly object State;

    /**
    <summary>Subscribes the out going data from this source to the specified
    handler.</summary>
    <param name="dh">Data that the subscriber wants passed to the handler on
    each call.</param>
    <param name="state">Data that the subscriber wants passed to the handler on
    each call.</param>
    */
    public Subscriber(IDataHandler dh, object state) {
      Handler = dh;
      State = state;
    }

    /**
    <summary>This is called to pass data to the subscribed handler.</summary>
    <param name="b">The data being passed.</param>
    <param name="retpath">A sender which can send data back to the source of B.
    </param>
    */
    public void Handle(MemBlock b, ISender retpath) {
      Handler.HandleData(b, retpath, State);
    }

    /**
    <summary>Can look up subscriptions based only on Handler equality.
    </summary>
    <param name="o">The object to compare for equality.</param>
    <returns>True if the handlers are equal, false otherwise.</returns>
    */
    public override bool Equals(object o) {
      Subscriber s = o as Subscriber;
      if( s != null ) {
        return (s.Handler == Handler);
      }
      else {
        return false;
      }
    }

    /**
    <summary>Returns the Handlers hashcode.</summary>
    <returns>The hashcode for the handler.</returns>
    */
    public override int GetHashCode() {
      return Handler.GetHashCode();
    }
  }

  /// <summary>Holds a single subscriber per a source.</summary>
  public class SimpleSource: ISource {
    /// <summary>Maps a source to a IDataHandler;
    protected Subscriber _sub;
    /// <summary>Lock to support multithreaded operations.</summary>
    protected Object _sync;

    /// <summary>Initializes the SimpleSource</summary>
    public SimpleSource() {
      _sync = new Object();
    }

    /**
    <summary>Subscribes the out going data from this source to the specified
    handler.</summary>
    <param name="hand">Data that the subscriber wants passed to the handler on
    each call.</param>
    <param name="state">Data that the subscriber wants passed to the handler on
    each call.</param>
    */
    public virtual void Subscribe(IDataHandler hand, object state) {
      lock(_sync) {
        _sub = new Subscriber(hand, state);
      }
    }

    /**
    <summary>Unsubscribes the the IDataHandler if it is the current
    IDatahandler.</summary>
    <param name="hand">An IDataHandler that wishes to remove itself as a
    destination for data coming from this source.</param>
    */
    public virtual void Unsubscribe(IDataHandler hand) {
      lock(_sync) {
        if( _sub.Handler == hand ) {
          _sub = null;
        }
        else {
          throw new Exception(String.Format("Handler: {0}, not subscribed", hand));
        }
      }
    }
  }

  /// <summary>Provides multiple subscribers of an ISource.</summary>
  public class MultiSource : ISource {
    /// <summary>A list of all the subscribers.</summary>
    /*
     * This needs to be volatile to avoid the lock in Announce.
     */
    protected volatile ArrayList _subs;
    /// <summary>A lock to allow for multithreaded operations.</summary>
    protected readonly Object _sync;

    /// <summary>Initializes a MultiSource</summary>
    public MultiSource() {
      _subs = new ArrayList();
      _sync = new object();
    }

    /**
    <summary>Subscribes the out going data from this source to the specified
    handler.</summary>
    <param name="hand">Data that the subscriber wants passed to the handler on
    each call.</param>
    <param name="state">Data that the subscriber wants passed to the handler on
    each call.</param>
    */
    public void Subscribe(IDataHandler h, object state) {
      Subscriber s = new Subscriber(h, state);
    //We have to lock so there is no race between the read and the write
      lock( _sync ) {
        _subs = Functional.Add(_subs, s);
      }
    }

    /**
    <summary>Unsubscribes the the IDataHandler if it is the current
    IDatahandler.</summary>
    <param name="hand">An IDataHandler that wishes to remove itself as a
    destination for data coming from this source.</param>
    */
    public void Unsubscribe(IDataHandler h) {
      Subscriber s = new Subscriber(h, null);
    //We have to lock so there is no race between the read and the write
      lock( _sync ) {
        int idx = _subs.IndexOf(s);
        _subs = Functional.RemoveAt(_subs, idx);
      }
    }

    /**
    <summary>Calls Handle on all IDataHandlers subscribed.</summary>
    <param name="b">The data being passed.</param>
    <param name="retpath">A sender which can send data back to the source of B.
    <returns> the number of Handlers that saw this data.</returns>
    */
    public int Announce(MemBlock b, ISender return_path) {
      ArrayList subs = _subs;
      int handlers = subs.Count;
      for(int i = 0; i < handlers; i++) {
        Subscriber s = (Subscriber) subs[i];
      //No need to lock since subs can never change
        s.Handle(b, return_path);
      }
      return handlers;
    }
  }

  /// <summary>Provides a demultiplexing table for MultiSource.</summary>
  public class DemuxHandler {
    /// <summary>A lock to allow for multithreaded operations.</summary>
    Object _sync;
    /// <summary>Holds the mapping of key to MultiSource.</summary>
    Hashtable _subscription_table;

    /// <summary>Initializes a DemuxHandler.</summary>
    public DemuxHandler() {
      _sync = new Object();
      _subscription_table = new Hashtable();
    }

    /**
    <summary>All packets that come to this are demultiplexed according to t.
    To subscribe or unsubscribe, get the ISource for the type you want and
    subscribe to it,</summary>
    <param name="t">The key for the MultiSource.</param>
   */
    public ISource GetTypeSource(Object t) {
    //It's safe to get from a Hashtable without a lock.
      ISource s = (ISource)_subscription_table[t];
      if( s == null ) {
        lock( _sync ) {
        //Since we last checked, there may be a ISource from another thread:
          s = (ISource)_subscription_table[t];
          if( s == null ) {
            s = new MultiSource();
            _subscription_table[t] = s;
          }
        }
      }
      return s;
    }

    /**
    <summary>Deletes (and thus unsubscribes) all IDataHandlers for a given key.
    </summary>
    <param name="t">The key for the MultiSource.</param>
    */
    public void ClearTypeSource(Object t) {
      lock( _sync ) {
        _subscription_table.Remove(t);
      }
    }

    /**
    <summary>Deletes (and thus unsubscribes) all IDataHandlers for the table.
    </summary>
    */
    public void Clear() {
      _subscription_table.Clear();
    }
  }
}
