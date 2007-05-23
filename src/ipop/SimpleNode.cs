#define HACK

using System;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using System.Threading;

using Brunet;
using Brunet.Dht;

/* The SimpleNode just works for a p2p router
 * (Doesn't generate or sink any packets)
 * Could sink; in case no route to destination is available!
 */
namespace Ipop {
  public class SimpleNode {
    private static Node node;
    private static FDht dht;
    private static Node [] nodes;
    private static SoapDht sd;
    private static Thread sdthread;
    private static bool one_run;
    private static ArrayList dhtfiles = new ArrayList();

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
              Console.WriteLine("-s must be followed by an existing file name.\n");
              PrintHelp();
            }
            config_file = args[index];
            break;
          case "-s":
            if(config_file != string.Empty || node_count > 1) {
              Console.WriteLine("-s cannot be used with -m or -c.\n");
              PrintHelp();
            }
            sd = SoapDhtClient.GetSoapDhtClient();
            soap_client = true;
            break;
          case "-dc":
            if(node_count > 1 || dhtfiles.Count > 0) {
              Console.WriteLine("-dc cannot be used with -m or -df.\n");
              PrintHelp();
            }
            dhtconsole = true;
            break;
          case "-df":
            index++;
            if(node_count > 1 || dhtconsole) {
              Console.WriteLine("-df cannot be used with -m or -dc.\n");
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
        while(true) Thread.Sleep(1000*60*60*24);
      }

      if(node != null) {
        node.Disconnect();
        Thread.Sleep(1000);
      }
      else if(nodes != null) {
        for(int i = 0; i < nodes.Length; i++) {
          nodes[i].Disconnect();
        }
        Thread.Sleep(1000);
      }

      Environment.Exit(0);
      return 0;
    }

    public static void StartBrunet(string config_file, int n) {
      if(n > 1) {
        nodes = new Node[n];
      }

      for(int i = 0; i < n; i++) {
        //configuration file 
        IPRouterConfig config = IPRouterConfigHandler.Read(config_file, true);

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
        FDht ndht = null;
        if (config.dht_media == null || config.dht_media.Equals("disk")) {
          ndht = new FDht(brunetNode, EntryFactory.Media.Disk, 3);
        } else if (config.dht_media.Equals("memory")) {
          ndht = new FDht(brunetNode, EntryFactory.Media.Memory, 3);
        }

        Console.Error.WriteLine("Calling Connect");

        brunetNode.Connect();
        if(n > 1) {
          nodes[i] = brunetNode;
        }
        else {
          node = brunetNode;
          dht = ndht;
        }

        if(config.EnableSoapDht && sdthread == null) {
          sdthread = SoapDhtServer.StartSoapDhtServerAsThread(dht);
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
          string password = null;
          if(dht != null) {
            password = DhtOp.Create(data.key, data.value, data.password, ttl, dht);
          }
          else {
            sd.Create(data.key, data.value, data.password, ttl);
          }
          if(password == null) {
            if(one_run && dhtfiles.Count == 1) {
              Console.WriteLine("Fail");
            }
            else if(dhtfiles.Count == 1) {
              Console.Error.WriteLine("DATA:::Dht operatin failed!");
            }
            return;
          }
          else if(password != data.password) {
            data.password = password;
            DhtDataHandler.Write(filename, data);
          }
          /* We exit if this was meant to try to create a data point */
          if(one_run && dhtfiles.Count == 1) {
            Console.WriteLine("Pass");
            return;
          }
          if(dhtfiles.Count == 1) {
            Console.Error.WriteLine("DATA:::Dht operation succeeded, sleeping for " + (ttl / 2));
          }
          System.Threading.Thread.Sleep((ttl / 2) * 1000);
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
            Console.Write("Enter password:  ");
            string password = Console.ReadLine();
            Console.Write("Enter TTL:  ");
            int ttl = Int32.Parse(Console.ReadLine());
            Console.WriteLine("Attempting Put() on key : " + key);
            if(dht != null) {
              DhtOp.Put(key, value, password, ttl, dht);
            }
            else {
              sd.Put(key, value, password, ttl);
            }
            Console.WriteLine("Operation Complete!");
          }
          else if (str_oper.Equals("Create")) {
            Console.Write("Enter key:  ");
            string key = Console.ReadLine();
            Console.Write("Enter data:  ");
            string value = Console.ReadLine();
            Console.Write("Enter password:  ");
            string password = Console.ReadLine();
            Console.Write("Enter TTL:  ");
            int ttl = Int32.Parse(Console.ReadLine());
            Console.WriteLine("Attempting Create() on key : " + key);
            string result = null;
            if(dht != null) {
              result = DhtOp.Create(key, value, password, ttl, dht);
            }
            else {
              result = sd.Create(key, value, password, ttl);
            }
            if(result == null) {
              Console.WriteLine("Operation Failed!");
            }
            else {
              Console.WriteLine("Operation Succeeded");
            }
          }
          else if (str_oper.Equals("Get")) {
            Console.Write("Enter key:  ");
            string key = Console.ReadLine();
            Hashtable[] results;
            if(dht != null) {
              results = DhtOp.Get(key, dht);
            }
            else {
              results = sd.Get(key);
            }

            Console.WriteLine("Number of results:  " + results.Length);
            Console.WriteLine("");

            for(int i = 0; i < results.Length; i++) {
              Hashtable ht = results[i];
              Console.WriteLine("Result:  " + i);
              Console.WriteLine("Value: " + ht["value_string"]);
              Console.WriteLine("Age:  " + ht["age"] + "\n");
            }
          }
          else if (str_oper.Equals("Done")) {
            System.Console.Error.WriteLine("Disconnecting...");
            break;
          }
        }
        catch (Exception) {
          Console.WriteLine("Dht may not be available, yet, try again now or a little later");
        }
      }
    }

    public static void PrintHelp() {
      Console.WriteLine("IPOP Interface 1.0");
      Console.WriteLine("\tCommands:");
      Console.WriteLine("\t-m %d  : Multipe Nodes can not be used with -s or -d requires -c");
      Console.WriteLine("\t-c %s  : Use a configuration file and create a Brunet Node for Dht access");
      Console.WriteLine("\t-s     : Use soap to access Dht");
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
