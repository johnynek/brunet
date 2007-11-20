using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using System.Threading;
using System.Net;

using Brunet;
using Brunet.Dht;

namespace Ipop {
  public class SimpleNode {
    private static SimpleNodeData [] simplenodes;
    private static IDht sd;
    private static bool one_run;
    private static ArrayList dhtfiles = new ArrayList();
    private static IPRouterConfig config;

    public static int Main(string []args) {
      int node_count = 1;
      string config_file = string.Empty;
      bool soap_client = false;
      bool dhtconsole = false;
      one_run = false;
      OSDependent.DetectOS();

      int index = 0;
      while(index < args.Length) {
        switch(args[index]) {
          case "-m":
            int new_node_count = 0;
            try {
              new_node_count = Int32.Parse(args[++index]);
            }
            catch{}

            if(dhtconsole || dhtfiles.Count > 0 || soap_client) {
              Console.Error.WriteLine("-m cannot be used with -df, -dc, or -s.\n");
              PrintHelp();
            }
            else if((index == args.Length) || new_node_count == 0) {
              Console.WriteLine("-m must be followed by a number.\n");
              PrintHelp();
            }
            node_count = new_node_count;
            if(node_count <= 1) {
              Console.WriteLine("-m must be followed by a number larger than 1.\n");
              PrintHelp();
            }
            break;
          case "-c":
            index++;
            if(soap_client) {
              Console.WriteLine("-c cannot be used with -s.\n");
              PrintHelp();
            }
            else if((index == args.Length) || !File.Exists(args[index])) {
              Console.WriteLine("-c must be followed by an existing file name.\n");
              PrintHelp();
            }
            config_file = args[index];
            break;
          case "-s":
            goto case "-x";
          case "-x":
            int dht_port = 0;
            try {
              dht_port = Int32.Parse(args[++index]);
            }
            catch{}

            if(config_file != string.Empty || node_count > 1 || dht_port == 0) {
              Console.WriteLine("{0} cannot be used with -m or -c and must be followed by the port number to use.\n", args[index-1]);
              PrintHelp();
            }
            if(args[index-1] == "-s") {
              sd = DhtServiceClient.GetSoapDhtClient(dht_port);
            }
            else {
              sd = DhtServiceClient.GetXmlRpcDhtClient(dht_port);
            }
            soap_client = true;
            break;
          case "-dc":
            if(node_count > 1 || dhtfiles.Count > 0) {
              Console.WriteLine("-dc cannot be used with -m, or -df.\n");
              PrintHelp();
            }
            dhtconsole = true;
            break;
          case "-df":
            index++;
            if(node_count > 1 || dhtconsole) {
              Console.WriteLine("-df cannot be used with -m, or -dc.\n");
              PrintHelp();
            }
            if((index != args.Length) && args[index] == "one_run") {
              if(dhtfiles.Count == 0) {
                one_run = true;
                index++;
              }
              else {
                Console.WriteLine("one_run cannot be used with multiple data files\n");
                PrintHelp();
              }
            }
            if((index == args.Length) || !File.Exists(args[index])) {
              Console.WriteLine("-df must be followed by an existing file name.\n");
              PrintHelp();
            }
            dhtfiles.Add(args[index]);
            break;
          case "-help":
          default:
            PrintHelp();
            break;
        }
        index++;
      }

      if(config_file != string.Empty) {
        StartBrunet(config_file, node_count);
      }

      if(config_file == string.Empty && !soap_client) {
        Console.WriteLine("Either a -s or -c must be used.\n");
        PrintHelp();
      }
      else if(dhtfiles.Count != 0) {
        Thread []threads = new Thread [dhtfiles.Count];
        for(int i = 0; i < dhtfiles.Count; i++) {
          threads[i] = new Thread(DhtProcess);
          threads[i].Start(i);
        }

        for(int i = 0; i < dhtfiles.Count; i++) {
          threads[i].Join();
        }
      }
      else if(dhtconsole) {
        DhtConsole();
      }
      else {
        while(true) {
          Thread.Sleep(1000*60*60*24*7);
          for(int i = 0; i < simplenodes.Length; i++)
            Console.Error.WriteLine("I am connected to {0} as {1}.  Current time is {2}.", 
              simplenodes[i].node.Realm, simplenodes[i].node.Address.ToString(), DateTime.UtcNow);
        }
      }

      if(simplenodes != null) {
        for(int i = 0; i < simplenodes.Length; i++) {
          simplenodes[i].node.Disconnect();
        }
        Thread.Sleep(1000);

        Environment.Exit(0);
      }

      return 0;
    }

    public static void StartBrunet(string config_file, int n) {
      simplenodes = new SimpleNodeData[n];

      try {
        config = IPRouterConfigHandler.Read(config_file);
      }
      catch {
        Console.WriteLine("Invalid or missing configuration file...");
        Environment.Exit(1);
      }

      for(int i = 0; i < n; i++) {
        simplenodes[i] = new SimpleNodeData();
      }
    }

    public static void  DhtProcess(object number) {
      string filename = dhtfiles[(int) number].ToString();
      DhtData data = DhtDataHandler.Read(filename);
       // Create a thread for each of these...
      if(data.key == null || data.value == null || data.ttl == null) {
        return;
      }
      int ttl = Int32.Parse(data.ttl);
      while(true) {
        try {
          if(dhtfiles.Count == 1) {
            Console.Error.WriteLine("DATA:::Attempting Dht operation!");
          }
          object oresult = null;
          bool result = false;
          try {
            if(simplenodes != null) {
              oresult = simplenodes[0].dht.Create(data.key, data.value, ttl);
            }
            else {
              oresult = sd.Create(data.key, data.value, ttl);
            }
            result = (bool) oresult;
          }
          catch (DhtException) {
            result = false;
          }
          catch (Exception x) {
            throw x;
          }
          if(one_run) {
            if(result) {
              Console.WriteLine("Pass");
            }
            else {
              Console.WriteLine("Fail");
            }
            return;
          }
          if(dhtfiles.Count == 1) {
            if(result)  {
              Console.Error.WriteLine("DATA:::Dht operation succeeded, sleeping for " + (ttl / 2));
              System.Threading.Thread.Sleep((ttl / 2) * 1000);
            }
            else {
              Console.Error.WriteLine("DATA:::Dht operation failed, sleeping for 30 seconds and retrying.");
              System.Threading.Thread.Sleep(30000);
            }
          }
        }
        catch(Exception) {
          if(dhtfiles.Count == 1) {
            Console.Error.WriteLine("DATA:::Dht operation failed, sleeping for 15 seconds and trying again.");
          }
          System.Threading.Thread.Sleep(15000);
        }
      }
    }

    public static void DhtConsole() {
      int oper_count = 0;
      while(true) {
        Console.Write("{0}: Enter operation (Get/Put/Create/Done):  ", oper_count++);
        string str_oper = Console.ReadLine();
        try {
          if (str_oper.Equals("Put")) {
            Console.Write("Enter key:  ");
            string key = Console.ReadLine();
            Console.Write("Enter data:  ");
            string value = Console.ReadLine();
            Console.Write("Enter TTL:  ");
            int ttl = Int32.Parse(Console.ReadLine());
            Console.WriteLine("Attempting Put() on key : " + key);
            bool result = false;
            if(simplenodes != null) {
              result = simplenodes[0].dht.Put(key, value, ttl);
            }
            else {
              result = sd.Put(key, value, ttl);
            }
            Console.WriteLine("Operation Complete and " + ((result) ? "Success" : "Failure"));
          }
          else if (str_oper.Equals("Create")) {
            Console.Write("Enter key:  ");
            string key = Console.ReadLine();
            Console.Write("Enter data:  ");
            string value = Console.ReadLine();
            Console.Write("Enter TTL:  ");
            int ttl = Int32.Parse(Console.ReadLine());
            Console.WriteLine("Attempting Create() on key : " + key);
            bool result = false;
            object oresult = null;
            try {
              if(simplenodes != null) {
                oresult = simplenodes[0].dht.Create(key, value, ttl);
              }
              else {
                oresult = sd.Create(key, value, ttl);
              }
              result = (bool) oresult;
            }
            catch (DhtException) {
              Console.WriteLine("Operation Completed and Failure because: " + oresult);
            }
            catch (Exception x) {
              throw x;
            }
            Console.WriteLine("Operation Completed and Success");
          }
          else if (str_oper.Equals("Get")) {
            Console.Write("Enter key:  ");
            string key = Console.ReadLine();
            if(simplenodes != null) {
              BlockingQueue queue = new BlockingQueue();
              simplenodes[0].dht.AsGet(key, queue);


              int count = 0;
              while(true) {
                DhtGetResult dgr;
                try {
                  dgr = (DhtGetResult) queue.Dequeue();
                }
                catch(Exception){
                  break;
                }

                Console.WriteLine("Result:  " + count++);
                Console.WriteLine("Value: " + dgr.valueString);
                Console.WriteLine("Age:  " + dgr.age);
                Console.WriteLine("Ttl:  " + dgr.ttl + "\n");
              }
              Console.WriteLine("Number of results: {0}\n", count);
            }
            else {
              string token = sd.BeginGet(key);
              int count = 0;
              DhtGetResult dgr;
              while(!(dgr = sd.ContinueGet(token)).IsEmpty()) {
                Console.WriteLine("Result:  " + count++);
                Console.WriteLine("Value: " + dgr.valueString);
                Console.WriteLine("Age:  " + dgr.age);
                Console.WriteLine("Ttl:  " + dgr.ttl + "\n");
              }
              Console.WriteLine("Number of results: {0}\n", count);
            }
          }
          else if (str_oper.Equals("Done")) {
            System.Console.Error.WriteLine("Disconnecting...");
            simplenodes[0].node.DepartureEvent -= simplenodes[0].DisconnectHandler;
            break;
          }
        }
        catch (System.Net.WebException e) {
          Console.Error.WriteLine("Soap/XmlRpc Dht service not available");
          Console.WriteLine(e);
        }
        catch (Exception e) {
          Console.WriteLine("Dht may not be available, yet, try again now or a little later.");
          Console.WriteLine(e);
        }
      }
    }

    public static void PrintHelp() {
      Console.WriteLine("IPOP Interface 1.0");
      Console.WriteLine("\tCommands:");
      Console.WriteLine("\t-m %d  : Multipe Nodes can not be used with -s or -d requires -c");
      Console.WriteLine("\t-c %s  : Use a configuration file and create a Brunet Node for Dht access");
      Console.WriteLine("\t-s     : Use soap to access Dht");
      Console.WriteLine("\t-x     : Use xmlrpc to access Dht");
      Console.WriteLine("\t-dc    : Enable the Dht Console");
      Console.WriteLine("\t-df %s : Dht data file");
      Console.WriteLine("\t-help  : Help (this screen)\n");
      Console.WriteLine("Examples:");
      Console.WriteLine("  100 nodes:\n    SimpleNode.exe -m 100 -c ipop.config");
      Console.WriteLine("  Storing data into the dht:\n    SimpleNode.exe -s -df data.cfg");
      Environment.Exit(1);
    }

    public class SimpleNodeData
    {
      private static readonly int sleep_min = 60, sleep_max = 3600;
      private int sleep = 60;
      private DateTime runtime;
      private StructuredNode _node;
      public StructuredNode node { get { return _node; } }
      private Dht _dht;
      public Dht dht { get { return _dht; } }

      public SimpleNodeData() {
        StartUp();
      }

      private void StartUp()
      {
        runtime = DateTime.UtcNow;
        _node = Brunet_Common.CreateStructuredNode(config);
        _dht = Brunet_Common.RegisterDht(node);
        Brunet_Common.StartServices(_node, _dht, config);
        new IpopInformation(node, "SimpleNode");
        node.DepartureEvent += DisconnectHandler;
        node.DisconnectOnOverload = true;
        Console.Error.WriteLine("I am connected to {0} as {1}",
                                config.brunet_namespace, node.Address.ToString());
        (new Thread(_node.Connect)).Start();
      }

      public void DisconnectHandler(object o, EventArgs ea)
      {
        (new Thread(new ThreadStart(SleepAndRestart))).Start();
      }

      private void SleepAndRestart()
      {
        if(simplenodes[0].node == _node) {
          Brunet_Common.RemoveHandlers(node);
        }
        _node = null;
        _dht = null;

        DateTime now = DateTime.UtcNow;
        Thread.Sleep(sleep * 1000);
        if(now - runtime < TimeSpan.FromSeconds(sleep_max)) {
          sleep *= 2;
          sleep = (sleep > sleep_max) ? sleep_max : sleep;
        }
        else {
          sleep /= 2;
          sleep = (sleep < sleep_min) ? sleep_min : sleep;
        }
        StartUp();
      }
    }
  }
}
