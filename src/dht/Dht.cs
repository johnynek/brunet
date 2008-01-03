/*
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Diagnostics;
using System.Text;
using System.Collections;
using System.Security.Cryptography;
using System.Threading;

using Brunet;

namespace Brunet.Dht {	
  public class DhtException: Exception {
    public DhtException(string message): base(message) {}
  }

  public class Dht {
    public static BooleanSwitch DhtLog =
        new BooleanSwitch("Dht", "Log for Dht!");
    protected object _sync = new object();
    private RpcManager _rpc;
    public Node node = null;
    public bool Activated { get { return _table.Activated; } }
    public readonly int DEGREE;
    public readonly int DELAY;
    public readonly int MAJORITY;
    public static readonly int MAX_BYTES = 1000;

    //table server
    protected TableServer _table;
    public int Count { get { return _table.Count; } }
    //Dht Get / Put States
    private volatile Hashtable _adps_table = new Hashtable();
    private volatile Hashtable _adgs_table = new Hashtable();

    //keep track of our current neighbors, we start with none
    protected AHAddress _left_addr = null, _right_addr = null;

    public Dht(Node node) {
      this.node = node;
      //get an instance of RpcManager for the node
      _rpc = RpcManager.GetInstance(node);
      _table = new TableServer(node, _rpc);
      //register the table with the RpcManagers
      _rpc.AddHandler("dht", _table);
      // We default into a single pair of nodes for each data point
      DEGREE = 1;
      MAJORITY = 1;
      // 60 second delay for blocking calls
      DELAY = 60000;
    }

    public Dht(Node node, int degree) :
      this(node){
      this.DEGREE = (int) System.Math.Pow(2, degree);
      this.MAJORITY = DEGREE / 2 + 1;
    }

    // Delay from users point of view is in seconds
    public Dht(Node node, int degree, int delay) :
      this(node, degree){
      DELAY = delay * 1000;
    }

    public BlockingQueue[] PrimitivePut(MemBlock key, int ttl, MemBlock data) {
      return PrimitivePut(key, ttl, data, false);
    }

    public BlockingQueue[] PrimitiveCreate(MemBlock key, int ttl, MemBlock data) {
      return PrimitivePut(key, ttl, data, true);
    }

    public BlockingQueue[] PrimitivePut(MemBlock key, int ttl, MemBlock data, bool unique) {
      if (!Activated) {
        throw new Exception("DhtClient: Not yet activated.");
      }

      BlockingQueue[] q = new BlockingQueue[DEGREE];
      MemBlock[] b = MapToRing(key);

      for(int i = 0; i < DEGREE; i++) {
        Address target = new AHAddress(b[i]);
        AHSender s = new AHSender(node , target);
        q[i] = new BlockingQueue();
        _rpc.Invoke(s, q[i], "dht.Put", b[i], data, ttl, unique);
      }
      return q;
    }

    public BlockingQueue[] PrimitiveGet(MemBlock key, int maxbytes, MemBlock token) {
      if (!Activated) {
        throw new Exception("DhtClient: Not yet activated.");
      }

      BlockingQueue[] q = new BlockingQueue[DEGREE];
      MemBlock[] b = MapToRing(key);

      for(int i = 0; i < DEGREE; i++) {
        Address target = new AHAddress(b[i]);
        AHSender s = new AHSender(node, target);
        q[i] = new BlockingQueue();
        _rpc.Invoke(s, q[i], "dht.Get", b[i], maxbytes, token);
      }
      return q;
    }

    /* Below are all the Create methods, they rely on a unique put   *
     * this returns true if it succeeded or an exception if it didn't */

    public void AsCreate(MemBlock key, MemBlock value, int ttl, Channel returns) {
      AsPut(key, value, ttl, returns, true);
    }

    public void AsCreate(string key, MemBlock value, int ttl, Channel returns) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      AsCreate(keyb, value, ttl, returns);
    }

    public void AsCreate(string key, string value, int ttl, Channel returns) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      MemBlock valueb = MemBlock.Reference(Encoding.UTF8.GetBytes(value));
      AsCreate(keyb, valueb, ttl, returns);
    }

    public bool Create(MemBlock key, MemBlock value, int ttl) {
      return Put(key, value, ttl, true);
    }

    public bool Create(string key, MemBlock value, int ttl) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      return Create(keyb, value, ttl);
    }

    public bool Create(string key, string value, int ttl) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      MemBlock valueb = MemBlock.Reference(Encoding.UTF8.GetBytes(value));
      return Create(keyb, valueb, ttl);
    }

    /* Below are all the Get methods */

    public void AsGet(string key, Channel returns) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      AsGet(keyb, returns);
    }

    public DhtGetResult[] Get(string key) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      return Get(keyb);
    }

    public DhtGetResult[] Get(MemBlock key) {
      BlockingQueue returns = new BlockingQueue();
      AsGet(key, returns);
      ArrayList allValues = new ArrayList();
      while(true) {
        // Still a chance for Dequeue to execute on an empty closed queue 
        // so we'll do this instead.
        try {
          DhtGetResult dgr = (DhtGetResult) returns.Dequeue();
          allValues.Add(dgr);
        }
        catch (Exception) {
          break;
        }
      }
      return (DhtGetResult []) allValues.ToArray(typeof(DhtGetResult));
    }

    //  This is the get that does all the work 
    public void AsGet(MemBlock key, Channel returns) {
      if (!Activated) {
        throw new Exception("DhtClient: Not yet activated.");
      }

      // create a GetState and map in our table map its queues to it
      // so when we get a GetHandler we know which state to load
      AsDhtGetState adgs = new AsDhtGetState(returns);
      Channel[] q = new Channel[DEGREE];
      lock(_adgs_table.SyncRoot) {
        for (int k = 0; k < DEGREE; k++) {
          Channel queue = new Channel();
          _adgs_table[queue] = adgs;
          q[k] = queue;
        }
      }

      // Setting up our Channels
      for (int k = 0; k < DEGREE; k++) {
        Channel queue = q[k];
        queue.CloseAfterEnqueue();
        queue.EnqueueEvent += this.GetEnqueueHandler;
        queue.CloseEvent += this.GetCloseHandler;
        adgs.queueMapping[queue] = k;
      }

      // Sending off the request!
      adgs.brunet_address_for_key = MapToRing(key);
      for (int k = 0; k < DEGREE; k++) {
        Address target = new AHAddress(adgs.brunet_address_for_key[k]);
        AHSender s = new AHSender(node, target, AHPacket.AHOptions.Greedy);
        _rpc.Invoke(s, q[k], "dht.Get", adgs.brunet_address_for_key[k], MAX_BYTES, null);
      }
    }

    /* Here we receive a Channel, use it to look up our state, process the results,
     * and update our state as necessary
     */

    public void GetEnqueueHandler(Object o, EventArgs args) {
      Channel queue = (Channel) o;
      // Looking up state
      AsDhtGetState adgs = (AsDhtGetState) _adgs_table[queue];

      if(adgs == null) {
        return;
      }

      int idx = (int) adgs.queueMapping[queue];
      // Test to see if we got any results and place them into results if necessary
      ISender sendto = null;
      MemBlock token = null;
      try {
        RpcResult rpc_reply = (RpcResult) queue.Dequeue();
        ArrayList result = (ArrayList) rpc_reply.Result;
        //Result may be corrupted
        ArrayList values = (ArrayList) result[0];
        int remaining = (int) result[1];
        if(remaining > 0) {
          token = (byte[]) result[2];
          sendto = rpc_reply.ResultSender;
        }

        // Going through the return values and adding them to our
        // results, if a majority of our servers say a data exists
        // we say it is a valid data and return it to the caller
        foreach (Hashtable ht in values) {
          MemBlock mbVal = (byte[]) ht["value"];
          int count = 1;
          Hashtable res = null;
          lock(adgs.SyncRoot) {
            res = (Hashtable) adgs.results[mbVal];
            if(res == null) {
              res = new Hashtable();
              adgs.results[mbVal] = res;
              adgs.ttls[mbVal] = ht["ttl"];
            }
            else {
              adgs.ttls[mbVal] = (int) adgs.ttls[mbVal] + (int) ht["ttl"];
            }

            res[idx] = true;
            count = ((ICollection) adgs.results[mbVal]).Count;
          }
          if(count == MAJORITY) {
            ht["ttl"] = (int) adgs.ttls[mbVal] / MAJORITY;
            adgs.returns.Enqueue(new DhtGetResult(ht));
          }
        }
      }
      catch (Exception) {
        sendto = null;
        token = null;
      }

    // We were notified that more results were available!  Let's go get them!
      if(token != null && sendto != null) {
        Channel new_queue = new Channel();
        lock(adgs.SyncRoot) {
          adgs.queueMapping[new_queue] = idx;
        }
        lock(_adgs_table.SyncRoot) {
          _adgs_table[new_queue] = adgs;
        }
        new_queue.CloseAfterEnqueue();
        new_queue.EnqueueEvent += this.GetEnqueueHandler;
        new_queue.CloseEvent += this.GetCloseHandler;
        try {
          _rpc.Invoke(sendto, new_queue, "dht.Get", 
                    adgs.brunet_address_for_key[idx], MAX_BYTES, token);
        }
        catch(Exception) {
          lock(adgs.SyncRoot) {
            adgs.queueMapping.Remove(new_queue);
          }
          lock(_adgs_table.SyncRoot) {
            _adgs_table.Remove(new_queue);
          }
          new_queue.EnqueueEvent -= this.GetEnqueueHandler;
          new_queue.CloseEvent -= this.GetCloseHandler;
        }
      }
    }

    private void GetCloseHandler(object o, EventArgs args) {
      Channel queue = (Channel) o;
      queue.EnqueueEvent -= this.GetEnqueueHandler;
      queue.CloseEvent -= this.GetCloseHandler;
      // Looking up state
      AsDhtGetState adgs = (AsDhtGetState) _adgs_table[queue];

      if(adgs == null) {
        return;
      }

      int count = 0;
      lock(adgs.SyncRoot) {
        adgs.queueMapping.Remove(queue);
        count = adgs.queueMapping.Count;
      }
      lock(_adgs_table.SyncRoot) {
        _adgs_table.Remove(queue);
      }
      if(count == 0) {
        adgs.returns.Close();
        GetFollowUp(adgs);
      }
      else if(count < MAJORITY && !adgs.GotToLeaveEarly) {
        lock(adgs.SyncRoot) {
          if(!adgs.GotToLeaveEarly) {
            GetLeaveEarly(adgs);
          }
        }
      }
    }

    /* This helps us leave the Get early if we either have no results or
    * our remaining results will not reach a majority due to too many nodes
    * missing data
    */
    private void GetLeaveEarly(AsDhtGetState adgs) {
      int left = adgs.queueMapping.Count;
      // Maybe we can leave early
      bool got_all_values = true;
      foreach (DictionaryEntry de in adgs.results) {
        int val = ((Hashtable) de.Value).Count;
        if(val < MAJORITY && ((val + left) >= MAJORITY)) {
          got_all_values = false;
          break;
        }
      }

      // If we got to leave early, we must clean up
      if(got_all_values) {
        if(Dht.DhtLog.Enabled) {
          ProtocolLog.Write(Dht.DhtLog, String.Format(
            "GetLeaveEarly found:left:total = {0}:{1}:{2}", 
            adgs.results.Count, left, DEGREE));
        }
        adgs.returns.Close();
        adgs.GotToLeaveEarly = true;
      }
    }

    /**
     * Restores any of the Dht results that don't return all their values.
     * We only get here at the end of a Dht return operation, no 
     * locks necessary!
     * @param adgs the async dht get state we're restoring
     */
    private void GetFollowUp(AsDhtGetState adgs) {
      foreach (DictionaryEntry de in adgs.results) {
        if(de.Value == null || de.Key == null) {
          continue;
        }

        Hashtable res = (Hashtable) de.Value;
        if(res.Count < MAJORITY || res.Count == DEGREE) {
          if(res.Count < MAJORITY) {
            if(Dht.DhtLog.Enabled) {
              ProtocolLog.Write(Dht.DhtLog, String.Format(
                "Failed get count:total = {0}:{1}", res.Count, DEGREE));
            }
          }
          res.Clear();
          continue;
        }
        MemBlock value = (MemBlock) de.Key;

        int ttl = (int) adgs.ttls[value] / res.Count;
        if(Dht.DhtLog.Enabled) {
          ProtocolLog.Write(Dht.DhtLog, String.Format(
            "Doing follow up put count:total = {0}:{1}", res.Count, DEGREE));
        }
        for(int i = 0; i < DEGREE; i++) {
          if(!res.Contains(i)) {
            MemBlock key = adgs.brunet_address_for_key[i];
            Channel queue = new Channel();
            Address target = new AHAddress(key);
            AHSender s = new AHSender(node, target, AHPacket.AHOptions.Greedy);
            try {
             _rpc.Invoke(s, queue, "dht.Put", key, value, ttl, false);
            }
            catch(Exception) {}
          }
        }
        res.Clear();
      }
      adgs.ttls.Clear();
      adgs.results.Clear();
    }

    /** Below are all the Put methods, they use a non-unique put */

    public void AsPut(MemBlock key, MemBlock value, int ttl, Channel returns) {
      AsPut(key, value, ttl, returns, false);
    }

    public void AsPut(string key, MemBlock value, int ttl, Channel returns) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      AsPut(keyb, value, ttl, returns);
    }

    public void AsPut(string key, string value, int ttl, Channel returns) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      MemBlock valueb = MemBlock.Reference(Encoding.UTF8.GetBytes(value));
      AsPut(keyb, valueb, ttl, returns);
    }

    public bool Put(MemBlock key, MemBlock value, int ttl) {
      return Put(key, value, ttl, false);
    }

    public bool Put(string key, MemBlock value, int ttl) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      return Put(keyb, value, ttl);
    }

    public bool Put(string key, string value, int ttl) {
      MemBlock keyb = MemBlock.Reference(Encoding.UTF8.GetBytes(key));
      MemBlock valueb = MemBlock.Reference(Encoding.UTF8.GetBytes(value));
      return Put(keyb, valueb, ttl);
    }

    /** Since the Puts and Creates are the same from the client side, we merge them into a
    single put that if unique is true, it is a create, otherwise a put */

    public bool Put(MemBlock key, MemBlock value, int ttl, bool unique) {
      BlockingQueue returns = new BlockingQueue();
      AsPut(key, value, ttl, returns, unique);
      object result = returns.Dequeue();
      try {
        return (bool) result;
      }
      catch {
        throw (DhtException) result;
      }
    }

    public void AsPut(MemBlock key, MemBlock value, int ttl, Channel returns, bool unique) {
      if (!Activated) {
        throw new Exception("DhtClient: Not yet activated.");
      }

      AsDhtPutState adps = new AsDhtPutState(returns);

      MemBlock[] brunet_address_for_key = MapToRing(key);
      Channel[] q = new Channel[DEGREE];
      lock(_adps_table.SyncRoot) {
        for (int k = 0; k < DEGREE; k++) {
          Channel queue = new Channel();
          _adps_table[queue] = adps;
          q[k] = queue;
        }
      }

      for (int k = 0; k < DEGREE; k++) {
        Channel queue = q[k];
        queue.CloseAfterEnqueue();
        queue.EnqueueEvent += this.PutEnqueueHandler;
        queue.CloseEvent += this.PutCloseHandler;
        adps.queueMapping[queue] = k;
      }

      for (int k = 0; k < DEGREE; k++) {
        Address target = new AHAddress(brunet_address_for_key[k]);
        AHSender s = new AHSender(node, target, AHPacket.AHOptions.Greedy);
        _rpc.Invoke(s, q[k], "dht.Put", brunet_address_for_key[k], value, ttl, unique);
      }
    }

    /* We receive an Channel use it to map to our state and update the 
     * necessary, we'll get this even after a user has received his value, so
     * that we can ensure all places in the ring actually get the data.  Should
     * timeout after 5 minutes though!
     */
    public void PutEnqueueHandler(Object o, EventArgs args) {
      Channel queue = (Channel) o;
      // Get our mapping
      AsDhtPutState adps = (AsDhtPutState) _adps_table[queue];
      if(adps == null) {
        return;
      }

      /* Check out results from our request and update the overall results
      * send a message to our client if we're done!
      */
      bool result = false;
      try {
        RpcResult rpcResult = (RpcResult) queue.Dequeue();
        result = (bool) rpcResult.Result;
      }
      catch (Exception) {}
      lock(adps.SyncRoot) {
        if(result) {
          // Once we get pcount to a majority, we ship off the result
          adps.pcount++;
          if(adps.pcount == MAJORITY) {
            adps.returns.Enqueue(true);
            adps.returns.Close();
          }
        }
        else {
          /* Once we get to ncount to 1 less than a majority, we ship off the
          * result, because we can't get pcount equal to majority any more!
          */
          adps.ncount++;
          if(adps.ncount == MAJORITY - 1 || 1 == DEGREE) {
            adps.returns.Enqueue(new DhtException("Put failed by negative " +
              "responses:  P/N/T : " + adps.pcount + "/" + adps.ncount + "/" +
              DEGREE));
            adps.returns.Close();
          }
        }
      }
    }

    public void PutCloseHandler(Object o, EventArgs args) {
      Channel queue = (Channel) o;
      queue.CloseEvent -= this.PutCloseHandler;
      queue.EnqueueEvent -= this.PutEnqueueHandler;
      // Get our mapping
      AsDhtPutState adps = (AsDhtPutState) _adps_table[queue];
      if(adps == null) {
        return;
      }

      lock(_adps_table.SyncRoot) {
        _adps_table.Remove(queue);
      }
      int count = 0;
      lock(adps.SyncRoot) {
        adps.queueMapping.Remove(queue);
        count = adps.queueMapping.Count;
      }
      if(count == 0) {
        if(!adps.returns.Closed) {
          adps.returns.Enqueue(new DhtException("Put failed by lack of " +
              "responses:  P/N/T : " + adps.pcount + "/" + adps.ncount + "/" +
              DEGREE));
          adps.returns.Close();
        }
      }
    }

    /* Get the hash of the first key and add 1/DEGREE * Address space
     * to each successive key
     */
    public MemBlock[] MapToRing(byte[] key) {
      MemBlock[] targets = new MemBlock[DEGREE];
      // Setup the first key
      HashAlgorithm algo = new SHA1CryptoServiceProvider();
      byte[] target = algo.ComputeHash(key);
      Address.SetClass(target, AHAddress._class);
      targets[0] = MemBlock.Reference(target);

      // Setup the rest of the keys
      BigInteger inc_addr = Address.Full/DEGREE;
      BigInteger curr_addr = new BigInteger(targets[0]);
      for (int k = 1; k < targets.Length; k++) {
        curr_addr = curr_addr + inc_addr;
        target = Address.ConvertToAddressBuffer(curr_addr);
        Address.SetClass(target, AHAddress._class);
        targets[k] = target;
      }
      return targets;
    }

    protected class AsDhtPutState {
      public object SyncRoot = new object();
      public Hashtable queueMapping = new Hashtable();
      public int pcount = 0, ncount = 0;
      public Channel returns;

      public AsDhtPutState(Channel returns) {
        this.returns = returns;
      }
    }

    protected class AsDhtGetState {
      public object SyncRoot = new object();
      public bool GotToLeaveEarly = false;
      public Hashtable ttls = new Hashtable();
      public Hashtable queueMapping = new Hashtable();
      public Hashtable results = new Hashtable();
      public Channel returns;
      public MemBlock[] brunet_address_for_key;

      public AsDhtGetState(Channel returns) {
        this.returns = returns;
      }
    }
  }
}
