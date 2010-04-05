/*
Copyright (C) 2009 David Wolinsky <davidiw@ufl.edu>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using Brunet;
using Brunet.Messaging;
using Brunet.Applications;
using Brunet.Util;
using Brunet.Symphony;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Brunet.Applications.Examples {
  /// <summary>This class show an example HelloWorld of Brunet using
  /// IDataHandler.  We inherit BasicNode and IDataHandler.  BasicNode
  /// provides access to Brunet in a clean manner and IDataHandler allows
  /// this class to be an end point for p2p communication.</summary>
  public class HelloWorldNodeDataHandler : BasicNode, IDataHandler {
    /// <summary>A PType, or packet type, allows us to specify a type of packet
    /// that we would like to receive from the underlying Brunet Node.  This
    /// is prepended to all outgoing packets and is removed on all incoming packets
    /// prior to their arrival at HandleData.</summary>
    public readonly PType HW;

    /// <summary>The only parameter to the constructor is a valid NodeConfig.
    /// We also register the PType here.</summary>
    public HelloWorldNodeDataHandler(NodeConfig node_config) : base(node_config) {
      HW = new PType("HelloWorld");
    }

    /// <summary>This is the only method declared by IDataHandler.  All packets that
    /// are of PType("HelloWorld") will arrive here.</summary>
    /// <param name="payload">The data portion of the packet.</summary>
    /// <param name="return_path">The return path to the sending node.</summary>
    /// <param name="state">Optional state that is specified during the
    /// subscribe state.</summary>
    public void HandleData(MemBlock payload, ISender return_path, object state) {
      Console.WriteLine(return_path + ": " + payload.GetString(System.Text.Encoding.UTF8));
    }

    /// <summary>This methods send some ICopyable data to the remote address.
    /// </summary>
    /// <param name="remote_addr">Remote Nodes are referenced by their P2P
    /// Address, typically of type AHAddress.</param>
    /// <param name="data">This is an ICopyable object which contains the data
    /// to send.</param>
    public void SendMessage(Address remote_addr, ICopyable data) {
      // This instantiates a multi-use method to sending to the remote node,
      // though we will only use it once.  It is VERY similar to UDP.
      AHExactSender sender = new AHExactSender(_node, remote_addr);
      // This is the process of actually sending the data.
      sender.Send(new CopyList(HW, data));
    }

    /// <summary>This is the work horse method.</summary>
    public override void Run() {
      // This handles the whole process of preparing the Brunet.Node.
      CreateNode();
      // Each Brunet.Node contains a DemuxHandler, this object allows us to
      // request that any message with a specific PType arrive here at the
      // HandleData method.  In this case, we want the PType("HelloWorld"),
      // to arrive here, and without state.
      _node.DemuxHandler.GetTypeSource(HW).Subscribe(this, null);

      // Services include XmlRpcManager and Dht over XmlRpcManager
      StartServices();
      // Start the Brunet.Node and allow it to connect to remote nodes
      Thread thread = new Thread(_node.Connect);
      thread.Start();

      // We finally are at the hello world
      // This is our address, you can copy and paste this locally and at other
      // sites to communicate
      Console.WriteLine("Your address is: " + _node.Address + "\n");

      // We will continue on, until we get to the Disconnected states.  Assumming
      // you are running this on a supported platform, that would be triggered 
      // initially by ctrl-c
      while(_node.ConState != Node.ConnectionState.Disconnected) {
        // First we need the address of the remote node
        Console.Write("Send message to: ");
        string address_string = Console.ReadLine().Trim(new char[] {' ', '\t'});
        Address addr = null;
        try {
          addr = AddressParser.Parse(address_string);
        } catch {
          Console.WriteLine("Invalid address!\n");
          continue;
        }

        // Get a message
        Console.Write("Message: ");
        string message = Console.ReadLine();

        //Call the Send Message passing in a MemBlock which happens to implement ICopyable
        SendMessage(addr, MemBlock.Reference(Encoding.UTF8.GetBytes(message)));
        Console.WriteLine("Sent...\n");
      }

      // Stops the XmlRpcManager and associated services
      StopServices();
    }
  }

  public class Runner {
    public static int Main(string [] args) {
      // We need a valid NodeConfig, these are the proper steps to ensure we get one
      if(args.Length < 1 || !File.Exists(args[0])) {
        Console.WriteLine("First argument must be a NodeConfig");
        return -1;
      }

      NodeConfig node_config = null;
      try {
        node_config = Utils.ReadConfig<NodeConfig>(args[0]);
      } catch (Exception e) {
        Console.WriteLine("Invalid NodeConfig file:");
        Console.WriteLine("\t" + e.Message);
        return -1;
      }

      // Instantiate a new inherited node of your choice
      HelloWorldNodeDataHandler hwn = new HelloWorldNodeDataHandler(node_config);
      // And run it... this hijacks the current thread, we'll return once the node disconnects
      hwn.Run();

      Console.WriteLine("Exiting...");

      return 0;
    }
  }
}
