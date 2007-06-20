using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using Brunet;

namespace Brunet {
  public class SimpleXmlRpcClient {
    public static void Main(string[] args) {
      bool log = false;
      int index = 0;
      while (index < args.Length) {
        switch (args[index]) {
          case "-h":
            string help = "Options: \n"
                        + "-logXml:\tPrint request and response XML";
            Console.WriteLine(help);
            index++;
            break;
          case "-logXml":
            log = true;
            index++;
            break;
          default:
            break;
        }
      }


      IXmlRpcManager xm = XmlRpcManagerClient.GetXmlRpcManager(log);
      int oper_count = 0;
      while (true) {
        string node;
        int ah_option;
        int max_to_wait;
        string method;
        int args_num;
        object[] method_args;
        try {          
          Console.WriteLine("{0} Target node address: ", oper_count++);
          node = Console.ReadLine();
          Console.WriteLine("AHOptions [0-5]=[AddClassDefault,Last,Path,Greedy,Annealing,Exact]: ");
          ah_option = Int32.Parse(Console.ReadLine());
          Console.WriteLine("MAX number of items to wait from BlockingQueue(input negative int for all possible result)");
          max_to_wait = Int32.Parse(Console.ReadLine());
          Console.WriteLine("Method name: ");
          method = Console.ReadLine();
          Console.WriteLine("Number of arguments");
          args_num = Int32.Parse(Console.ReadLine());
          method_args = new object[args_num];
          for (int i = 0; i < args_num; i++) {
            Console.WriteLine("Arg #{0}", i);
            string arg = Console.ReadLine();
            Console.WriteLine("Type of arg #{0}: (AssemblyQualifiedName for Non-.Net Framework types)", i);
            string str_type = Console.ReadLine().Trim();
            Type arg_type = Type.GetType(str_type, true, true);
            Type[] itfs = arg_type.GetInterfaces();
            ArrayList list = new ArrayList(itfs);
            if(list.Contains(typeof(IConvertible))) {
              method_args[i] = Convert.ChangeType(arg, Type.GetType(str_type, true, true));
            } else if (arg_type == typeof(System.Byte[]) || arg_type == typeof(MemBlock)) {
              method_args[i] = Encoding.UTF8.GetBytes(arg);
            } else {
              throw new Exception("Unable to convert to the specified type");
            }
          }
        } catch(Exception e) {
          Console.WriteLine(e);
          continue;
        }

        //ready to call
        try {
          object[] rets = xm.proxy(node, ah_option, max_to_wait, method, method_args);
          Console.WriteLine("Got Response!");
          for (int i = 0; i < rets.Length; i++) {
            Console.WriteLine("Result #{0}:\n {1}", i, rets[i]);
          }
        } catch (Exception e) {
          Console.WriteLine(e);
          continue;
        }
      }
    }
  }
}
