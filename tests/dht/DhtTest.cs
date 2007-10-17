using System;
using System.Text;
using System.Collections;
using System.Security.Cryptography;
using System.Threading;

using Brunet;
using Brunet.Dht;

namespace Brunet.Dht {
  public class DhtOpTester {
    SortedList nodes = new SortedList();
    SortedList dhts = new SortedList();
    Dht default_dht;
    static readonly int degree = 3;
    static int network_size = 60;
    static readonly string brunet_namespace = "testing";
    static readonly int base_port = 55123;
    static readonly int value_size = 500;
    // Well this is needed because C# doesn't lock the console
    private object _lock = new object();

    public void ParallelCreate(byte[][] key, byte[][] value, int[] ttl,
                            bool[] expected_result, ref int op) {
      ArrayList threadlist = new ArrayList();
      for (int i = 0; i < key.Length; i++) {
        Hashtable ht = new Hashtable();
        ht.Add("key", key[i]);
        ht.Add("value", value[i]);

        ht.Add("ttl", ttl[i]);
        ht.Add("result", expected_result[i]);
        ht.Add("op", op++);
        Thread thread = new Thread(SerialCreate);
        thread.Start((object) ht);
        threadlist.Add(thread);
      }
      foreach(Thread thread in threadlist) {
        thread.Join();
      }
    }

    public void SerialCreate(byte[] key, byte[] value, int ttl,
                            bool expected_result, int op) {
      Hashtable ht = new Hashtable();
      ht.Add("key", key);
      ht.Add("value", value);
      ht.Add("ttl", ttl);
      ht.Add("result", expected_result);
      ht.Add("op", op);
      SerialCreate((object) ht);
    }

    public void SerialCreate(object data) {
      Hashtable ht = (Hashtable) data;
      byte[] key = (byte[]) ht["key"];
      byte[] value = (byte[]) ht["value"];
      int ttl = (int) ht["ttl"];
      int op = (int) ht["op"];
      bool expected_result = (bool) ht["result"];
      bool result = false;
      try {
        result = default_dht.Create(key, value, ttl);
      }
      catch {
        result = false;
      }
      if(result != expected_result) {
        if(!result) {
          lock(_lock) {
            Console.WriteLine("Possible failure from unsuccessful Create: " + op);
          }
        }
       else {
          lock(_lock) {
            Console.WriteLine("Possible failure from successful Create: " + op);
          }
        }
      }
    }

    public void ParallelGet(byte [][] key, byte[][][] result,
                            ref int op) {
      ArrayList threadlist = new ArrayList();
      for (int i = 0; i < key.Length; i++) {
        Hashtable ht = new Hashtable();
        ht.Add("key", key[i]);
        ht.Add("results", result[i]);
        ht.Add("op", op++);
        Thread thread = new Thread(SerialGet);
        thread.Start((object) ht);
        threadlist.Add(thread);
      }
      foreach(Thread thread in threadlist) {
        thread.Join();
      }
    }

    public void SerialGet(byte[] key, byte[][] results, int op) {
      Hashtable ht = new Hashtable();
      ht.Add("key", key);
      ht.Add("results", results);
      ht.Add("op", op);
      SerialGet((object) ht);
    }

    public void SerialGet(object data) {
      Hashtable ht = (Hashtable) data;
      byte[] key = (byte[]) ht["key"];
      byte[][] expected_results = (byte[][]) ht["results"];
      int op = (int) ht["op"];
      try {
        DhtGetResult[] result = default_dht.Get(key);
        bool found = false;
        int found_count = 0;
        for(int i = 0; i < result.Length; i++) {
          for(int j = 0; j < expected_results.Length; j++) {
            if(ArrayComparer(result[i].value, expected_results[j])) {
              found = true;
              break;
            }
          }
          if(found) {
            found_count++;
            found =  false;
          }
        }
        if(found_count != expected_results.Length) {
          lock(_lock) {
            Console.WriteLine("Failed get... attempted to get " + 
                expected_results.Length + " found " + found_count +
                " out of " + result.Length + " returned" +
                " operation: " + op);
          }
        }
      }
      catch(Exception e) {
        Console.WriteLine("Failure at operation: " + op);
        Console.WriteLine(e);
      }
    }

    public void SerialAsGet(byte[] key, byte[][] results, int op) {
      Hashtable ht = new Hashtable();
      ht.Add("key", key);
      ht.Add("results", results);
      ht.Add("op", op);
      SerialAsGet((object) ht);
    }

    public void SerialAsGet(object data) {
      Hashtable ht = (Hashtable) data;
      byte[] key = (byte[]) ht["key"];
      byte[][] expected_results = (byte[][]) ht["results"];
      int op = (int) ht["op"];
      try {
        BlockingQueue queue = new BlockingQueue();
        default_dht.AsGet(key, queue);
        bool found = false;
        int found_count = 0;
        while(true) {
          DhtGetResult dgr = null;
          try {
            dgr = (DhtGetResult) queue.Dequeue();
          }
          catch(Exception){
              break;
          }
          for(int j = 0; j < expected_results.Length; j++) {
            if(ArrayComparer(dgr.value, expected_results[j])) {
              found = true;
              break;
            }
          }
          if(found) {
            found_count++;
            found =  false;
          }
        }
        if(found_count != expected_results.Length) {
          lock(_lock) {
            Console.WriteLine("Failed get... attempted to get " + 
                expected_results.Length + " found " + found_count +
                " operation: " + op);
          }
        }
      }
      catch(Exception e) {
        Console.WriteLine("Failure at operation: " + op);
        Console.WriteLine(e);
        throw e;
      }
    }

    public void ParallelPut(byte[][] key, byte[][] value, int[] ttl,
                            bool[] expected_result, ref int op) {
      ArrayList threadlist = new ArrayList();
      for (int i = 0; i < key.Length; i++) {
        Hashtable ht = new Hashtable();
        ht.Add("key", key[i]);
        ht.Add("value", value[i]);
        ht.Add("ttl", ttl[i]);
        ht.Add("result", expected_result[i]);
        ht.Add("op", op++);
        Thread thread = new Thread(SerialPut);
        thread.Start((object) ht);
        threadlist.Add(thread);
      }
      foreach(Thread thread in threadlist) {
        thread.Join();
      }
    }

    public void SerialPut(byte[] key, byte[] value, int ttl,
                            bool expected_result, int op) {
      Hashtable ht = new Hashtable();
      ht.Add("key", key);
      ht.Add("value", value);
      ht.Add("ttl", ttl);
      ht.Add("result", expected_result);
      ht.Add("op", op);
      SerialPut((object) ht);
    }

    public void SerialPut(object data) {
      Hashtable ht = (Hashtable) data;
      byte[] key = (byte[]) ht["key"];
      byte[] value = (byte[]) ht["value"];
      int ttl = (int) ht["ttl"];
      bool expected_result = (bool) ht["result"];
      int op = (int) ht["op"];
      bool result = false;
      try {
        result = default_dht.Put(key, value, ttl);
      }
      catch {
        result = false;
      }
      if(result != expected_result) {
        if(!result) {
          lock(_lock) {
            Console.WriteLine("Possible failure from unsuccessful Put: " + op);
          }
        }
        else {
          lock(_lock) {
            Console.WriteLine("Possible failure from successful Put: " + op);
          }
        }
      }
    }

    public void Init() {
      Console.WriteLine("Initializing...");
      ArrayList RemoteTA = new ArrayList();
      for(int i = 0; i < network_size; i++) {
        RemoteTA.Add(TransportAddressFactory.CreateInstance("brunet.udp://localhost:" + (base_port + i)));
      }

      for(int i = 0; i < network_size; i++) {
        Address addr = (Address) new AHAddress(new RNGCryptoServiceProvider());
        Node node = new StructuredNode((AHAddress) addr, brunet_namespace);
        nodes.Add(addr, node);
        node.AddEdgeListener(new UdpEdgeListener(base_port + i));
        node.RemoteTAs = RemoteTA;
        node.Connect();
        dhts.Add(addr, new Dht(node, degree));
        if(i < network_size / ((Dht)dhts.GetByIndex(i)).DEGREE) {
          ((Dht)dhts.GetByIndex(i)).debug = true;
        }
      }
      default_dht = (Dht) dhts.GetByIndex(0);
    }

    // Checks the ring for completeness
    public bool CheckAllConnections() {
      Console.WriteLine("Checking ring...");
      Address start_addr = (Address) nodes.GetKeyList()[0];
      Address curr_addr = start_addr;

      for (int i = 0; i < network_size; i++) {
        Node node = (Node) nodes[curr_addr];
        ConnectionTable con_table = node.ConnectionTable;
        Connection con = con_table.GetLeftStructuredNeighborOf((AHAddress) curr_addr);
        Console.WriteLine("Hop {2}\t Address {0}\n\t Connection to left {1}\n", curr_addr, con, i);

        if (con == null) {
          Console.WriteLine("Found disconnection at position {0}.", i);
          return false;
        }
        Address next_addr = con.Address;

        if (next_addr == null) {
          Console.WriteLine("Found disconnection at position {0}.", i);
          return false;
        }

        con = null;
        try {
          con = ((Node)nodes[next_addr]).ConnectionTable.GetRightStructuredNeighborOf((AHAddress) next_addr);
        }
        catch {}
        if (con == null) {
          Console.WriteLine("Found disconnection at position {0}.", i);
          return false;
        }
        Address left_addr = con.Address;
        if(left_addr == null) {
          Console.WriteLine("Found disconnection.");
        }
        if(!curr_addr.Equals(left_addr)) {
          Console.WriteLine(curr_addr + " != " + left_addr);
          Console.WriteLine("Right had edge, but left has no record of it at {0}!", i);
          return false;
        }
        else if(next_addr.Equals(start_addr) && i != network_size -1) {
          Console.WriteLine("Completed circle too early.  Only " + (i + 1) + " nodes in the ring.");
          return false;
        }
        curr_addr = next_addr;
      }
      if(start_addr.Equals(curr_addr)) {
        Console.WriteLine("Ring properly formed!");
        return true;
      }
      return false;
    }

    public static string ArrayToString(byte[] array) {
      string result = string.Empty;
      for(int i = 0; i < array.Length; i++) {
        result += array[i];
      }
      return result;
    }

    public static bool ArrayComparer(byte[] array0, byte[] array1) {
      if(array0.Length != array1.Length) {
        return false;
      }
      for(int i = 0; i < array0.Length; i++) {
        if(array0[i] != array1[i]) {
          return false;
        }
      }
      return true;
    }

    public void Shutdown() {
      foreach(DictionaryEntry de in nodes) {
        Node node = (Node) de.Value;
        node.Disconnect();
      }
      Thread.Sleep(10);
    }

    public void SystemTest() {
      int op = 0;
      try {
        Console.WriteLine("The following are serial tests until mentioned otherwise.");
        DateTime start = DateTime.UtcNow;
        Test0(ref op);
        Test1(ref op);
        Test2(ref op);
        Test3(ref op);
        Test4(ref op);
        Test5(ref op);
        Test6(ref op);
        Test7(ref op);
        Test8(ref op);
        Test9(ref op);
        Test10(ref op);
        Test11(ref op);
        Test12(ref op);
        Test13(ref op);
        Test14(ref op);
        Console.WriteLine("Total memory: " + GC.GetTotalMemory(true));
        Console.WriteLine("Total time: " + (DateTime.UtcNow - start));
      }
      catch (Exception e) {
        Console.WriteLine("Failure at operation: " + (op - 1));
        Console.WriteLine(e);
        return;
      }
    }

    public void Test0(ref int op) {
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[value_size];
      byte[][] results = new byte[1][];

      Console.WriteLine("Test 0: Testing 1 put and 1 get");
      rng.GetBytes(key);
      rng.GetBytes(value);
      this.SerialPut(key, value, 3000, true, op++);
      Console.WriteLine("Insertion done...");
      results[0] = value;
      this.SerialGet(key, results, op++);
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test1(ref int op) {
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[value_size];
      byte[][] results = new byte[1][];

      Console.WriteLine("Test 1: Testing 10 puts and 10 gets with different " +
          "keys serially.");
      for(int i = 0; i < 10; i++) {
        rng.GetBytes(key);
        rng.GetBytes(value);
        this.SerialPut(key, value, 3000, true, op++);
        Console.WriteLine("Insertion done...");
        results = new byte[1][];
        results[0] = value;
        this.SerialGet(key, results, op++);
      }
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test2(ref int op) {
      Console.WriteLine("Test 2: Testing 10 puts and 10 gets with the same key.");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[value_size];
      rng.GetBytes(key);
      ArrayList al_results = new ArrayList();

      for(int i = 0; i < 10; i++) {
        value = new byte[value_size];
        rng.GetBytes(value);
        this.SerialPut(key, value, 3000, true, op++);
        al_results.Add(value);
        Console.WriteLine("Insertion done...");
        this.SerialGet(key, (byte[][]) al_results.ToArray(typeof(byte[])), op++);
      }
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test3(ref int op) {
      Console.WriteLine("Test 3: Testing 1000 puts and 1 get with 1000 " +
          "results with the same key.");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[value_size];
      rng.GetBytes(key);
      ArrayList al_results = new ArrayList();
      BlockingQueue[] results_queue = new BlockingQueue[60];

      for(int i = 0; i < 60; i++) {
        value = new byte[value_size];
        rng.GetBytes(value);
        al_results.Add(value);
        results_queue[i] = new BlockingQueue();
        default_dht.AsPut(key, value, 3000, results_queue[i]);
      }
      for (int i = 0; i < 60; i++) {
        try {
          bool res = (bool) results_queue[i].Dequeue();
          Console.WriteLine("success in put : " + i);
        }
        catch {
          Console.WriteLine("Failure in put : " + i);
        }
      }
      Console.WriteLine("Insertion done...");
      this.SerialAsGet(key, (byte[][]) al_results.ToArray(typeof(byte[])), op++);
      Thread.Sleep(5000);
      Console.WriteLine("This checks to make sure our follow up Puts succeeded");
      this.SerialAsGet(key, (byte[][]) al_results.ToArray(typeof(byte[])), op++);
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test4(ref int op) {
      Console.WriteLine("Test 4: Testing 10 parallel puts and 1 get with the" +
           " same key.");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[value_size];
      byte[][] keys = new byte[10][];
      byte[][] values = new byte[10][];
      int[] ttls = new int[10];
      bool[] put_results = new bool[10];

      key = new byte[10];
      rng.GetBytes(key);

      for(int i = 0; i < 10; i++) {
        keys[i] = key;
        value = new byte[value_size];
        rng.GetBytes(value);
        values[i] = value;
        ttls[i] = 3000;
        put_results[i] = true;
      }
      this.ParallelPut(keys, values, ttls, put_results, ref op);
      Console.WriteLine("Insertion done...");
      this.SerialGet(key, values, op++);
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test5(ref int op) {
      Console.WriteLine("Test 5: Testing 10 parallel puts and 10 parallel " +
          "gets with the same key.");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[value_size];
      byte[][] keys = new byte[10][];
      byte[][] values = new byte[10][];
      int[] ttls = new int[10];
      byte[][][] gresults = new byte[10][][];
      bool[] put_results = new bool[10];

      key = new byte[10];
      rng.GetBytes(key);

      for(int i = 0; i < 10; i++) {
        keys[i] = key;
        value = new byte[value_size];
        rng.GetBytes(value);
        values[i] = value;
        gresults[i] = values;
        ttls[i] = 3000;
        put_results[i] = true;
      }
      this.ParallelPut(keys, values, ttls, put_results, ref op);
      Console.WriteLine("Insertion done...");
      this.ParallelGet(keys, gresults, ref op);
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test6(ref int op) {
      Console.WriteLine("Test 6: Testing 10 parallel puts and 10 parallel " +
          "gets with the different keys.");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key;
      byte[] value;
      byte[][] keys = new byte[10][];
      byte[][] values = new byte[10][];
      int[] ttls = new int[10];
      byte[][][] gresults = new byte[10][][];
      byte[][] results;
      bool[] put_results = new bool[10];

      for(int i = 0; i < 10; i++) {
        key = new byte[10];
        rng.GetBytes(key);
        keys[i] = key;
        value = new byte[value_size];
        rng.GetBytes(value);
        values[i] = value;
        results = new byte[1][];
        results[0] = value;
        gresults[i] = results;
        ttls[i] = 3000;
        put_results[i] = true;
      }
      this.ParallelPut(keys, values, ttls, put_results, ref op);
      Console.WriteLine("Insertion done...");
      this.ParallelGet(keys, gresults, ref op);
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test7(ref int op) {
      Console.WriteLine("Test 7: Testing Dht Put for uniqueness ... same key");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] value;
      byte[] key = new byte[10];
      rng.GetBytes(key);
      byte[][] results = new byte[1][];

      value = new byte[value_size];
      rng.GetBytes(value);
      this.SerialPut(key, value, 3000, true, op++);
      results = new byte[1][];
      results[0] = value;

      value = new byte[value_size];
      rng.GetBytes(value);
      this.SerialPut(key, value, 3000, false, op++);
      Console.WriteLine("Insertion done...");
      this.SerialGet(key, results, op++);

      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test8(ref int op) {
      Console.WriteLine("Test 8: Testing Dht Put for time idempotency ... " +
          "same key and same value");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] value;
      byte[] key = new byte[10];
      rng.GetBytes(key);
      byte[][] results = new byte[1][];

      value = new byte[value_size];
      rng.GetBytes(value);
      this.SerialPut(key, value, 3000, true, op++);
      results = new byte[1][];
      results[0] = value;

      this.SerialPut(key, value, 3000, true, op++);
      Console.WriteLine("Insertion done...");
      this.SerialGet(key, results, op++);

      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test9(ref int op) {
      Console.WriteLine("Test 9: Testing 10 parallel creates and 1 get " +
          "with the same key.");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[value_size];
      byte[][] keys = new byte[10][];
      byte[][] values = new byte[10][];
      int[] ttls = new int[10];
      bool[] create_results = new bool[10];

      key = new byte[10];
      rng.GetBytes(key);

      for(int i = 0; i < 10; i++) {
        keys[i] = key;
        value = new byte[value_size];
        rng.GetBytes(value);
        values[i] = value;
        ttls[i] = 3000;
        create_results[i] = true;
      }
      this.ParallelCreate(keys, values, ttls, create_results, ref op);
      Console.WriteLine("Insertion done...");
      this.SerialGet(key, values, op++);
      Console.WriteLine("This test is kind of bogus, but we'll either get 10" +
          " or 11 failure messages any less and we have a bug, this is for" +
          " operations up to " + (op - 1));
    }

    public void Test10(ref int op) {
      Console.WriteLine("Test 10: Testing 10 parallel creates and 10 " +
          "parallel gets with the different keys.");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key;
      byte[] value;
      byte[][] keys = new byte[10][];
      byte[][] values = new byte[10][];
      int[] ttls = new int[10];
      byte[][][] gresults = new byte[10][][];
      byte[][] results;
      bool[] create_results = new bool[10];

      for(int i = 0; i < 10; i++) {
        key = new byte[10];
        rng.GetBytes(key);
        keys[i] = key;
        value = new byte[value_size];
        rng.GetBytes(value);
        values[i] = value;
        results = new byte[1][];
        results[0] = value;
        gresults[i] = results;
        ttls[i] = 3000;
        create_results[i] = true;
      }
      this.ParallelCreate(keys, values, ttls, create_results, ref op);
      Console.WriteLine("Insertion done...");
      this.ParallelGet(keys, gresults, ref op);
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test11(ref int op) {
      Console.WriteLine("Test 11: Testing Dht Create for uniqueness ... same key");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] value;
      byte[] key = new byte[10];
      rng.GetBytes(key);
      byte[][] results = new byte[1][];

      value = new byte[value_size];
      rng.GetBytes(value);
      this.SerialCreate(key, value, 3000, true, op++);
      results = new byte[1][];
      results[0] = value;

      value = new byte[value_size];
      rng.GetBytes(value);
      this.SerialCreate(key, value, 3000, false, op++);
      Console.WriteLine("Insertion done...");
      this.SerialGet(key, results, op++);

      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test12(ref int op) {
      Console.WriteLine("Test 12: Testing Dht Create for time idempotency " +
          "... same key and same value");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] value;
      byte[] key = new byte[10];
      rng.GetBytes(key);
      byte[][] results = new byte[1][];

      value = new byte[value_size];
      rng.GetBytes(value);
      this.SerialCreate(key, value, 3000, true, op++);
      results = new byte[1][];
      results[0] = value;

      this.SerialCreate(key, value, 3000, true, op++);
      Console.WriteLine("Insertion done...");
      this.SerialGet(key, results, op++);

      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test13(ref int op) {
      Console.WriteLine("Test 13: Testing 10 parallel puts and 1 get with the" +
          " same key, we should get none back as they are meant to expire " +
          "before the get.");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[value_size];
      byte[][] keys = new byte[10][];
      byte[][] values = new byte[10][];
      int[] ttls = new int[10];
      bool[] put_results = new bool[10];

      key = new byte[10];
      rng.GetBytes(key);

      for(int i = 0; i < 10; i++) {
        keys[i] = key;
        value = new byte[value_size];
        rng.GetBytes(value);
        values[i] = value;
        if(i > 7) {
          ttls[i] = 15;
        }
        else {
          ttls[i] = 500;
        }
        put_results[i] = true;
      }
      this.ParallelPut(keys, values, ttls, put_results, ref op);
      Console.WriteLine("Insertion done...");
      Thread.Sleep(5000);
      this.SerialGet(key, values, op++);
      Console.WriteLine("Next get 2 should fail!");
      Thread.Sleep(20000);
      this.SerialGet(key, values, op++);
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
      Console.WriteLine("Every entry should be deleted by now...");
    }

    public void Test14(ref int op) {
      Console.WriteLine("Test 14: Testing 1000 puts and 1 get with 1000 " +
          "results with the same key.  Then we remove the main owner of the " +
          "key.");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[value_size];
      rng.GetBytes(key);
      ArrayList al_results = new ArrayList();
      int count = 60;
      BlockingQueue[] results_queue = new BlockingQueue[count];

      for(int i = 0; i < count; i++) {
        value = new byte[value_size];
        rng.GetBytes(value);
        al_results.Add(value);
        results_queue[i] = new BlockingQueue();
        default_dht.AsPut(key, value, 3000, results_queue[i]);
      }
      for (int i = 0; i < count; i++) {
        try {
          bool res = (bool) results_queue[i].Dequeue();
          Console.WriteLine("success in put : " + i);
        }
        catch {
          Console.WriteLine("Failure in put : " + i);
        }
      }
      Console.WriteLine("Insertion done...");
      Console.WriteLine("Disconnecting nodes...");
      MemBlock[] b = default_dht.MapToRing(key);
      BigInteger[] baddrs = new BigInteger[default_dht.DEGREE];
      BigInteger[] addrs = new BigInteger[default_dht.DEGREE];
      bool first_run = true;
      foreach(DictionaryEntry de in nodes) {
        Address addr = (Address) de.Key;
        for(int j = 0; j < b.Length; j++) {
          if(first_run) {
            addrs[j] = addr.ToBigInteger();
            baddrs[j] = (new AHAddress(b[j])).ToBigInteger();
          }
          else {
            BigInteger caddr = addr.ToBigInteger();
            BigInteger new_diff = baddrs[j] - caddr;
            if(new_diff < 0) {
              new_diff *= -1;
            }
            BigInteger c_diff = baddrs[j] - addrs[j];
            if(c_diff < 0) {
              c_diff *= -1;
            }
            if(c_diff > new_diff) {
              addrs[j] = caddr;
            }
          }
        }
        first_run = false;
      }

      for(int i = 0; i < addrs.Length; i++) {
        Console.WriteLine(new AHAddress(baddrs[i]) + " " + new AHAddress(addrs[i]));
        Address laddr = new AHAddress(addrs[i]);
        Node node = (Node) nodes[laddr];
        node.Disconnect();
        nodes.Remove(laddr);
        dhts.Remove(laddr);
        network_size--;
      }

      default_dht = (Dht) dhts.GetByIndex(0);

      // Checking the ring every 5 seconds..
      do  { Thread.Sleep(5000);}
      while(!CheckAllConnections());
      Console.WriteLine("Going to sleep now...");
      Thread.Sleep(15000);
      Console.WriteLine("Timeout done.... now attempting gets");
      this.SerialAsGet(key, (byte[][]) al_results.ToArray(typeof(byte[])), op++);
      Thread.Sleep(5000);
      Console.WriteLine("This checks to make sure our follow up Puts succeeded");
      this.SerialAsGet(key, (byte[][]) al_results.ToArray(typeof(byte[])), op++);
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
      foreach(DictionaryEntry de in dhts) {
        Dht dht = (Dht) de.Value;
        Console.WriteLine("Count ... " + dht.Count);
      }
    }

    public static void Main() {
      DhtOpTester dot = new DhtOpTester();
      dot.Init();
      do  { Thread.Sleep(5000);}
      while(!dot.CheckAllConnections());
      dot.SystemTest();
      dot.Shutdown();
      return;
    }
  }
}
