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
    private static Node [] nodes;
    private static Dht [] dhts;
    private static IDht sd;
    private static Thread sdthread;
    private static bool one_run;
    private static ArrayList dhtfiles = new ArrayList();
    private static bool tracker;
    private static IEnumerable addresses;

    public static int Main(string []args) {
      int node_count = 1;
      string config_file = string.Empty;
      bool soap_client = false;
      bool dhtconsole = false;
      one_run = false;
      tracker = false;

      OSDependent.DetectOS();

      int index = 0;
      while(index < args.Length) {
        switch(args[index]) {
          case "-m":
            int new_node_count = 0;
            index++;
            if(dhtconsole || dhtfiles.Count > 0 || soap_client) {
              Console.WriteLine("-m cannot be used with -df, -dc, or -s.\n");
              PrintHelp();
            }
            else if((index == args.Length) || !Int32.TryParse(args[index], out new_node_count)) {
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
            if(config_file != string.Empty || node_count > 1 || tracker) {
              Console.WriteLine("-s cannot be used with -m, -c, or -t.\n");
              PrintHelp();
            }
            sd = DhtServiceClient.GetSoapDhtClient();
            soap_client = true;
            break;
          case "-x":
            if (config_file != string.Empty || node_count > 1 || tracker)
            {
              Console.WriteLine("-x cannot be used with -m, -c, or -t.\n");
              PrintHelp();
            }
            sd = DhtServiceClient.GetXmlRpcDhtClient();
            soap_client = true;
            break;
          case "-dc":
            if(node_count > 1 || dhtfiles.Count > 0 || tracker) {
              Console.WriteLine("-dc cannot be used with -m, -df, or -t.\n");
              PrintHelp();
            }
            dhtconsole = true;
            break;
          case "-df":
            index++;
            if(node_count > 1 || dhtconsole || tracker) {
              Console.WriteLine("-df cannot be used with -m, -dc, or -t.\n");
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
          case "-t":
            if(soap_client || dhtconsole || dhtfiles.Count > 0) {
              Console.WriteLine("-t cannot be used with -s, -dc, or -df.\n");
              PrintHelp();
            }
            tracker = true;
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
        if(tracker) {
          while(true) {
            UpdateTracker();
            Thread.Sleep(1000*60*60);
          }
        }
        else {
          while(true) Thread.Sleep(1000*60*60*24);
        }
      }

      if(nodes != null) {
        for(int i = 0; i < nodes.Length; i++) {
          nodes[i].Disconnect();
        }
        Thread.Sleep(1000);

        Environment.Exit(0);
      }

      return 0;
    }

    public static void StartBrunet(string config_file, int n) {
      nodes = new Node[n];
      dhts = new Dht[n];

      //configuration file 
      IPRouterConfig config = IPRouterConfigHandler.Read(config_file, true);
      addresses = OSDependent.GetIPAddresses(config.DevicesToBind);

      for(int i = 0; i < n; i++) {
        //local node
        Node brunetNode = new StructuredNode(IPOP_Common.GenerateAHAddress(),
          config.brunet_namespace);
        //Where do we listen 
        Brunet.EdgeListener el = null;
        foreach(EdgeListener item in config.EdgeListeners) {
          int port = Int32.Parse(item.port) + i;
          if(config.DevicesToBind == null) {
            if (item.type =="tcp")
              el = new TcpEdgeListener(port);
            else if (item.type == "udp")
              el = new UdpEdgeListener(port);
            else if (item.type == "udp-as")
              el = new ASUdpEdgeListener(port);
            else
              throw new Exception("Unrecognized transport: " + item.type);
          }
          else {
  /*          if (item.type =="tcp")
              el = new TcpEdgeListener(port, (IEnumerable) (new IPAddresses(config.DevicesToBind)), null);*/
            if (item.type == "udp")
              el = new UdpEdgeListener(port, OSDependent.GetIPAddresses(config.DevicesToBind));
  /*          else if (item.type == "udp-as")
              el = new ASUdpEdgeListener(port, (IEnumerable) (new IPAddresses(config.DevicesToBind)), null);*/
            else
              throw new Exception("Unrecognized transport: " + item.type);
          }
          brunetNode.AddEdgeListener(el);
        }
        el = new TunnelEdgeListener(brunetNode);
        brunetNode.AddEdgeListener(el);

        //Here is where we connect to some well-known Brunet endpoints
        ArrayList RemoteTAs = new ArrayList();
        foreach(string ta in config.RemoteTAs)
          RemoteTAs.Add(TransportAddressFactory.CreateInstance(ta));
        brunetNode.RemoteTAs = RemoteTAs;

        //following line of code enables DHT support inside the SimpleNode
        Dht ndht = null;
        if (config.dht_media == null || config.dht_media.Equals("disk")) {
          ndht = new Dht(brunetNode, EntryFactory.Media.Disk, 3, 20);
        } else if (config.dht_media.Equals("memory")) {
          ndht = new Dht(brunetNode, EntryFactory.Media.Memory, 3, 20);
        }

        brunetNode.Connect();
        Console.Error.WriteLine("Called Connect, I am " + brunetNode.Address.ToString());
        nodes[i] = brunetNode;
        dhts[i] = ndht;

        if(config.EnableSoapDht && sdthread == null) {
          sdthread = DhtServer.StartDhtServerAsThread(dhts[0]);
        }
      }
    }

    // Get our eth0 IP address then post that and our Brunet address to the Dht
    public static void UpdateTracker() {
      string value = string.Empty;
      foreach (IPAddress address in addresses) {
        value += "|" + address.ToString();
      }

      for (int i = 0; i < nodes.Length; i++) {
        while(true) {
          bool result = false;
          try {
            result = dhts[i].Put("plab_tracker", nodes[i].Address.ToString() + value, 7200);
          }
          catch(Exception) {;}
          if(result) {
            break;
          }
          else {
            Thread.Sleep(10000);
          }
        }
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
          bool result = false;
          if(dhts != null) {
            result = dhts[0].Create(data.key, data.value, ttl);
          }
          else {
            result = sd.Create(data.key, data.value, ttl);
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
            if(dhts != null) {
              result = dhts[0].Put(key, value, ttl);
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
            if(dhts != null) {
              result = dhts[0].Create(key, value, ttl);
            }
            else {
              result = sd.Create(key, value, ttl);
            }
            Console.WriteLine("Operation Completed and " + ((result) ? "Success" : "Failure"));
          }
          else if (str_oper.Equals("Get")) {
            Console.Write("Enter key:  ");
            string key = Console.ReadLine();
            if(dhts != null) {
              BlockingQueue queue = dhts[0].AsGet(key);


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
                Console.WriteLine("Age:  " + dgr.age + "\n");
              }
              Console.WriteLine("Number of results: {0}\n", count);
            }
            else {
              DhtGetResult[] results = sd.Get(key);
              Console.WriteLine("Number of results: " + results.Length);
              Console.WriteLine("");

              for(int i = 0; i < results.Length; i++) {
                Console.WriteLine("Result: " + i);
                Console.WriteLine("Value: " + results[i].valueString);
                Console.WriteLine("Age: " + results[i].age + "\n");
              }
            }
          }
          else if (str_oper.Equals("Done")) {
            System.Console.Error.WriteLine("Disconnecting...");
            break;
          }
        }
        catch (System.Net.WebException) {
          Console.Error.WriteLine("Soap/XmlRpc Dht service not available");
        }
        catch (Exception) {
          Console.WriteLine("Dht may not be available, yet, try again now or a little later.");
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
  }
}
