using System;
using System.Text;
using System.Collections;
using System.Security.Cryptography;
using System.Threading;

using Brunet;
using Brunet.Dht;

namespace Brunet.Dht {
  public class DhtOpTester {
    Node []nodes;
    Dht []dhts;
    static readonly int degree = 2;
    static readonly int network_size = 20;
    static readonly string brunet_namespace = "testing";
    static readonly int base_port = 55123;
    // Well this is needed because C# doesn't lock the console
    private object _lock = new object();

    public void ParallelCreate(byte[][] key, byte[][] value, int[] ttl,
                            int[] index, bool[] expected_result, ref int op) {
      ArrayList threadlist = new ArrayList();
      for (int i = 0; i < key.Length; i++) {
        Hashtable ht = new Hashtable();
        ht.Add("key", key[i]);
        ht.Add("value", value[i]);

        ht.Add("ttl", ttl[i]);
        ht.Add("index", index[i]);
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
                            int index, bool expected_result, int op) {
      Hashtable ht = new Hashtable();
      ht.Add("key", key);
      ht.Add("value", value);
      ht.Add("ttl", ttl);
      ht.Add("index", index);
      ht.Add("result", expected_result);
      ht.Add("op", op);
      SerialCreate((object) ht);
    }

    public void SerialCreate(object data) {
      Hashtable ht = (Hashtable) data;
      byte[] key = (byte[]) ht["key"];
      byte[] value = (byte[]) ht["value"];
      int ttl = (int) ht["ttl"];
      int index = (int) ht["index"];
      int op = (int) ht["op"];
      bool expected_result = (bool) ht["result"];
      bool result = dhts[index].Create(key, value, ttl);
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

    public void ParallelGet(byte [][] key, int[] index, byte[][][] result,
                            ref int op) {
      ArrayList threadlist = new ArrayList();
      for (int i = 0; i < key.Length; i++) {
        Hashtable ht = new Hashtable();
        ht.Add("key", key[i]);
        ht.Add("index", index[i]);
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

    public void SerialGet(byte[] key, int index, byte[][] results, int op) {
      Hashtable ht = new Hashtable();
      ht.Add("key", key);
      ht.Add("index", index);
      ht.Add("results", results);
      ht.Add("op", op);
      SerialGet((object) ht);
    }

    public void SerialGet(object data) {
      Hashtable ht = (Hashtable) data;
      byte[] key = (byte[]) ht["key"];
      int index = (int) ht["index"];
      byte[][] expected_results = (byte[][]) ht["results"];
      int op = (int) ht["op"];
      try {
        DhtGetResult[] result = dhts[index].Get(key);
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
                " operation: " + op);
          }
        }
      }
      catch(Exception e) {
        Console.WriteLine("Failure at operation: " + op);
        Console.WriteLine(e);
      }
    }

    public void SerialAsGet(byte[] key, int index, byte[][] results, int op) {
      Hashtable ht = new Hashtable();
      ht.Add("key", key);
      ht.Add("index", index);
      ht.Add("results", results);
      ht.Add("op", op);
      SerialAsGet((object) ht);
    }

    public void SerialAsGet(object data) {
      Hashtable ht = (Hashtable) data;
      byte[] key = (byte[]) ht["key"];
      int index = (int) ht["index"];
      byte[][] expected_results = (byte[][]) ht["results"];
      int op = (int) ht["op"];
      try {
        BlockingQueue queue = dhts[index].AsGet(key);
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
            Console.WriteLine("Found result {0} / {1}", found_count, expected_results.Length);
            found =  false;
          }
          else {
            Console.WriteLine("Not found result {0} / {1}", found_count, expected_results.Length);
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
      }
    }

    public void ParallelPut(byte[][] key, byte[][] value, int[] ttl,
                            int[] index, bool[] expected_result, ref int op) {
      ArrayList threadlist = new ArrayList();
      for (int i = 0; i < key.Length; i++) {
        Hashtable ht = new Hashtable();
        ht.Add("key", key[i]);
        ht.Add("value", value[i]);
        ht.Add("ttl", ttl[i]);
        ht.Add("index", index[i]);
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
                            int index, bool expected_result, int op) {
      Hashtable ht = new Hashtable();
      ht.Add("key", key);
      ht.Add("value", value);
      ht.Add("ttl", ttl);
      ht.Add("index", index);
      ht.Add("result", expected_result);
      ht.Add("op", op);
      SerialPut((object) ht);
    }

    public void SerialPut(object data) {
      Hashtable ht = (Hashtable) data;
      byte[] key = (byte[]) ht["key"];
      byte[] value = (byte[]) ht["value"];
      int ttl = (int) ht["ttl"];
      int index = (int) ht["index"];
      bool expected_result = (bool) ht["result"];
      int op = (int) ht["op"];
      bool result = dhts[index].Put(key, value, ttl);
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
      nodes = new Node[network_size];
      dhts = new Dht[network_size];
      for(int i = 0; i < network_size; i++) {
        nodes[i] = new StructuredNode(new AHAddress(new RNGCryptoServiceProvider()), brunet_namespace);
        nodes[i].AddEdgeListener(new UdpEdgeListener(base_port + i));
        ArrayList RemoteTA = new ArrayList();
        int port = base_port + ((i + 1) % (network_size - 1));
        RemoteTA.Add(TransportAddressFactory.CreateInstance("brunet.udp://localhost:" + port));
        nodes[i].RemoteTAs = RemoteTA;
        nodes[i].Connect();
        dhts[i] = new Dht(nodes[i], EntryFactory.Media.Disk, degree);
      }
    }

    // Checks the ring for completeness
    public bool CheckAllConnections() {
      Console.WriteLine("Checking ring...");
      Dht curr_dht = dhts[0];
      Address start_addr = curr_dht.Address;
      Address curr_addr = start_addr;

      for (int i = 0; i < network_size; i++) {
        curr_addr = curr_dht.LeftAddress;
        if (curr_addr == null) {
          Console.WriteLine("Found disconnection.");
          return false;
        }
        else if(curr_addr.Equals(start_addr) && i != network_size -1) {
          int count = i + 1;
          Console.WriteLine("Completed circle too early.  Only " + count + " nodes in the ring.");
          return false;
        }

        foreach (Dht dht in dhts) {
          if (dht.Address.Equals(curr_addr)) {
            curr_dht = dht;
            break;
          }
        }
      }
      if(start_addr.Equals(curr_dht.Address)) {
        Console.WriteLine("Ring properly formed!");
        return true;
      }
      else {
        Console.WriteLine("Ring is not properly formed.");
        return false;
      }
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
      foreach(Node node in nodes) {
        node.Disconnect();
      }
    }

    public void SystemTest() {
      int op = 0;
      try {
        Console.WriteLine("The following are serial tests until mentioned otherwise.");
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
      byte[] value = new byte[10];
      byte[][] results = new byte[1][];

      Console.WriteLine("Test 0: Testing 1 put and 1 get");
      rng.GetBytes(key);
      rng.GetBytes(value);
      rng.GetBytes(value);
      this.SerialPut(key, value, 3000, 0, true, op++);
      results[0] = value;
      this.SerialGet(key, 0, results, op++);
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test1(ref int op) {
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[10];
      byte[][] results = new byte[1][];

      Console.WriteLine("Test 1: Testing 10 puts and 10 gets with different " +
          "keys serially.");
      for(int i = 0; i < 10; i++) {
        rng.GetBytes(key);
        rng.GetBytes(value);
        this.SerialPut(key, value, 3000, 0, true, op++);
        results = new byte[1][];
        results[0] = value;
        this.SerialGet(key, 0, results, op++);
      }
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test2(ref int op) {
      Console.WriteLine("Test 2: Testing 10 puts and 10 gets with the same key.");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[10];
      rng.GetBytes(key);
      ArrayList al_results = new ArrayList();

      for(int i = 0; i < 10; i++) {
        value = new byte[10];
        rng.GetBytes(value);
        this.SerialPut(key, value, 3000, 0, true, op++);
        al_results.Add(value);
        this.SerialGet(key, 0, (byte[][]) al_results.ToArray(typeof(byte[])), op++);
      }
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test3(ref int op) {
      Console.WriteLine("Test 3: Testing 1000 puts and 1 get with 1000 " +
          "results with the same key.");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[10];
      rng.GetBytes(key);
      ArrayList al_results = new ArrayList();
      BlockingQueue[] results_queue = new BlockingQueue[40];

      for(int i = 0; i < 40; i++) {
        value = new byte[10];
        rng.GetBytes(value);
        al_results.Add(value);
        results_queue[i] = dhts[0].AsPut(key, value, 3000);
      }
      for (int i = 0; i < 40; i++) {
        bool result = (bool) results_queue[i].Dequeue();
        if(result == false) {
          Console.WriteLine("Failure in put : " + i);
        }
        else {
          Console.WriteLine("success in put : " + i);
        }
      }
      this.SerialAsGet(key, 0, (byte[][]) al_results.ToArray(typeof(byte[])), op++);
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test4(ref int op) {
      Console.WriteLine("Test 4: Testing 10 parallel puts and 1 get with the" +
           " same key.");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[10];
      byte[][] keys = new byte[10][];
      byte[][] values = new byte[10][];
      int[] ttls = new int[10];
      int[] dhtindexes = new int[10];
      bool[] put_results = new bool[10];

      key = new byte[10];
      rng.GetBytes(key);

      for(int i = 0; i < 10; i++) {
        keys[i] = key;
        value = new byte[10];
        rng.GetBytes(value);
        values[i] = value;
        ttls[i] = 3000;
        dhtindexes[i] = 0;
        put_results[i] = true;
      }
      this.ParallelPut(keys, values, ttls, dhtindexes, put_results, ref op);
      this.SerialGet(key, 0, values, op++);
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test5(ref int op) {
      Console.WriteLine("Test 5: Testing 10 parallel puts and 10 parallel " +
          "gets with the same key.");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[10];
      byte[][] keys = new byte[10][];
      byte[][] values = new byte[10][];
      int[] ttls = new int[10];
      int[] dhtindexes = new int[10];
      byte[][][] gresults = new byte[10][][];
      bool[] put_results = new bool[10];

      key = new byte[10];
      rng.GetBytes(key);

      for(int i = 0; i < 10; i++) {
        keys[i] = key;
        value = new byte[10];
        rng.GetBytes(value);
        values[i] = value;
        gresults[i] = values;
        ttls[i] = 3000;
        dhtindexes[i] = 0;
        put_results[i] = true;
      }
      this.ParallelPut(keys, values, ttls, dhtindexes, put_results, ref op);
      this.ParallelGet(keys, dhtindexes, gresults, ref op);
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
      int[] dhtindexes = new int[10];
      byte[][][] gresults = new byte[10][][];
      byte[][] results;
      bool[] put_results = new bool[10];

      for(int i = 0; i < 10; i++) {
        key = new byte[10];
        rng.GetBytes(key);
        keys[i] = key;
        value = new byte[10];
        rng.GetBytes(value);
        values[i] = value;
        results = new byte[1][];
        results[0] = value;
        gresults[i] = results;
        ttls[i] = 3000;
        dhtindexes[i] = 0;
        put_results[i] = true;
      }
      this.ParallelPut(keys, values, ttls, dhtindexes, put_results, ref op);
      this.ParallelGet(keys, dhtindexes, gresults, ref op);
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test7(ref int op) {
      Console.WriteLine("Test 7: Testing Dht Put for uniqueness ... same key");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] value;
      byte[] key = new byte[10];
      rng.GetBytes(key);
      byte[][] results = new byte[1][];

      value = new byte[10];
      rng.GetBytes(value);
      this.SerialPut(key, value, 3000, 0, true, op++);
      results = new byte[1][];
      results[0] = value;

      value = new byte[10];
      rng.GetBytes(value);
      this.SerialPut(key, value, 3000, 0, false, op++);

      this.SerialGet(key, 0, results, op++);

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

      value = new byte[10];
      rng.GetBytes(value);
      this.SerialPut(key, value, 3000, 0, true, op++);
      results = new byte[1][];
      results[0] = value;

      this.SerialPut(key, value, 3000, 0, true, op++);

      this.SerialGet(key, 0, results, op++);

      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test9(ref int op) {
      Console.WriteLine("Test 9: Testing 10 parallel creates and 1 get " +
          "with the same key.");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[10];
      byte[][] keys = new byte[10][];
      byte[][] values = new byte[10][];
      int[] ttls = new int[10];
      int[] dhtindexes = new int[10];
      bool[] create_results = new bool[10];

      key = new byte[10];
      rng.GetBytes(key);

      for(int i = 0; i < 10; i++) {
        keys[i] = key;
        value = new byte[10];
        rng.GetBytes(value);
        values[i] = value;
        ttls[i] = 3000;
        dhtindexes[i] = 0;
        create_results[i] = true;
      }
      this.ParallelCreate(keys, values, ttls, dhtindexes, create_results, ref op);
      this.SerialGet(key, 0, values, op++);
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
      int[] dhtindexes = new int[10];
      byte[][][] gresults = new byte[10][][];
      byte[][] results;
      bool[] create_results = new bool[10];

      for(int i = 0; i < 10; i++) {
        key = new byte[10];
        rng.GetBytes(key);
        keys[i] = key;
        value = new byte[10];
        rng.GetBytes(value);
        values[i] = value;
        results = new byte[1][];
        results[0] = value;
        gresults[i] = results;
        ttls[i] = 3000;
        dhtindexes[i] = 0;
        create_results[i] = true;
      }
      this.ParallelCreate(keys, values, ttls, dhtindexes, create_results, ref op);
      this.ParallelGet(keys, dhtindexes, gresults, ref op);
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test11(ref int op) {
      Console.WriteLine("Test 11: Testing Dht Create for uniqueness ... same key");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] value;
      byte[] key = new byte[10];
      rng.GetBytes(key);
      byte[][] results = new byte[1][];

      value = new byte[10];
      rng.GetBytes(value);
      this.SerialCreate(key, value, 3000, 0, true, op++);
      results = new byte[1][];
      results[0] = value;

      value = new byte[10];
      rng.GetBytes(value);
      this.SerialCreate(key, value, 3000, 0, false, op++);

      this.SerialGet(key, 0, results, op++);

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

      value = new byte[10];
      rng.GetBytes(value);
      this.SerialCreate(key, value, 3000, 0, true, op++);
      results = new byte[1][];
      results[0] = value;

      this.SerialCreate(key, value, 3000, 0, true, op++);

      this.SerialGet(key, 0, results, op++);

      Console.WriteLine("If no error messages successful up to: " + (op - 1));
    }

    public void Test13(ref int op) {
      Console.WriteLine("Test 13: Testing 10 parallel puts and 1 get with the" +
          " same key, we should get none back as they are meant to expire " +
          "before the get.");
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] key = new byte[10];
      byte[] value = new byte[10];
      byte[][] keys = new byte[10][];
      byte[][] values = new byte[10][];
      int[] ttls = new int[10];
      int[] dhtindexes = new int[10];
      bool[] put_results = new bool[10];

      key = new byte[10];
      rng.GetBytes(key);

      for(int i = 0; i < 10; i++) {
        keys[i] = key;
        value = new byte[10];
        rng.GetBytes(value);
        values[i] = value;
        ttls[i] = 60;
        dhtindexes[i] = 0;
        put_results[i] = true;
      }
      this.ParallelPut(keys, values, ttls, dhtindexes, put_results, ref op);
      Thread.Sleep(20000);
      this.SerialGet(key, 0, values, op++);
      Console.WriteLine("Next get should all fail!");
      Thread.Sleep(60000);
      this.SerialGet(key, 0, values, op++);
      Console.WriteLine("If no error messages successful up to: " + (op - 1));
      Console.WriteLine("Every entry should be deleted by now...");
    }

    public static void Main() {
      DhtOpTester dot = new DhtOpTester();
      dot.Init();
      while(!dot.CheckAllConnections()) { Thread.Sleep(5000);}
      dot.SystemTest();
      dot.Shutdown();
      return;
    }
  }
}
