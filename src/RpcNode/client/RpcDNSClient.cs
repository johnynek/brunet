using Brunet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;

public class RpcDNSClient {
  /// <summary>The BrunetRpc used in communicating with the RpcNode.</summary>
  protected BrunetRpc _brpc;
  /// <summary>The remote end point of our RpcIpopNode.</summary>
  protected EndPoint _remEP;
  /// <summary>Array of IP Addresses containing the loopback address.</summary>
  protected IPAddress[] loopback;

  /// <summary>Creates a new RpcDNSClient.</summary>
  public RpcDNSClient() {
    _brpc = new BrunetRpc();
    loopback = new IPAddress[1] { IPAddress.Loopback };
    _remEP = null;
  }

  /// <summary>
  /// This method looks for the RpcIpopNode running on the localhost and
  /// sets an Endpoint for future unicast communication
  /// </summary>
  public void Init() {
    BlockingQueue q = new BlockingQueue();
    _brpc.Rpc.Invoke(_brpc.IPHandler.CreateMulticastSender(loopback), q, "RpcIpopNode.CheckInstance");
    while (true) {
      try {
        RpcResult res = (RpcResult)q.Dequeue();
        _remEP = ((UnicastSender)res.ResultSender).EndPoint;
        if ((bool)res.Result) {
          break;
        }
      }
      catch (InvalidOperationException e) {
        Console.WriteLine(e.Message);
        break;
      }
      catch (Exception e) {
        Console.WriteLine(e.Message);
        continue;
      }
    }
  }

  /// <summary>
  /// This method uses the sys:link.GetNeighbors rpc call to get the 
  /// brunet address of the RpcIpopNode on the localhost
  /// </summary>
  /// <returns>A string representation of the brunet address</returns>
  public string GetAddress() {
    BlockingQueue q = new BlockingQueue();
    _brpc.Rpc.Invoke(_brpc.IPHandler.CreateUnicastSender(_remEP), q, "sys:link.GetNeighbors");
    try {
      RpcResult res = (RpcResult)q.Dequeue();
      Hashtable hash = (Hashtable)res.Result;
      Console.WriteLine(_remEP + ":" + ((UnicastSender)res.ResultSender).EndPoint);
      foreach (DictionaryEntry de in hash) {
        Console.WriteLine("{0} = {1}", de.Key, de.Value);
      }
      return (string)hash["self"];
    }
    catch (InvalidOperationException e) {
      Console.WriteLine(e.Message);
    }
    catch (Exception e) {
      Console.WriteLine(e.Message);
    }
    return null;
  }

  /// <summary>
  /// Asynchronous get address method, use when making asynchronous calls
  /// </summary>
  public void AsGetAddress() {
    Channel q = new Channel();
    q.CloseAfterEnqueue();

    // Delegate function gets called when close event occurs
    q.CloseEvent += delegate(Object o, EventArgs eargs) {
      RpcResult res = (RpcResult)q.Dequeue();
      Hashtable hash = (Hashtable)res.Result;
      Console.WriteLine(_remEP + ":" + ((UnicastSender)res.ResultSender).EndPoint);
      foreach (DictionaryEntry de in hash) {
        Console.WriteLine("{0} = {1}", de.Key, de.Value);
      }
    };
    _brpc.Rpc.Invoke(_brpc.IPHandler.CreateUnicastSender(_remEP), q, "sys:link.GetNeighbors");
  }

  /// <summary>
  /// This mehod allows the client to add a buddy to the network which 
  /// is used for DNS, ip translation
  /// </summary>
  /// <param name="name">A string DNS name used to register the address</param>
  /// <param name="address">A string brunet address</param>
  /// <returns>A string IP address for the added name</returns>
  public string AddBuddy(string name, string address) {
    BlockingQueue q = new BlockingQueue();
    _brpc.Rpc.Invoke(_brpc.IPHandler.CreateUnicastSender(_remEP), q, "RpcIpopNode.RegisterMapping", name, address);
    try {
      RpcResult res = (RpcResult)q.Dequeue();
      Console.WriteLine(_remEP + ":" + ((UnicastSender)res.ResultSender).EndPoint);
      Console.WriteLine(res.Result);
      return (string)res.Result;
    }
    catch (InvalidOperationException e) {
      Console.WriteLine(e.Message);
    }
    catch (Exception e) {
      Console.WriteLine(e.Message);
    }
    return null;
  }

  /// <summary>
  /// This method checks to see if a buddy is accessible on the network
  /// </summary>
  /// <param name="address">A brunet address representation</param>
  /// <returns>A boolean to determine if network is accessible</returns>
  public bool CheckBuddy(string address) {
    BlockingQueue q = new BlockingQueue();
    _brpc.Rpc.Invoke(_brpc.IPHandler.CreateUnicastSender(_remEP), q, "RpcIpopNode.CheckBuddy", address);
    try {
      RpcResult res = (RpcResult)q.Dequeue();
      Console.WriteLine(_remEP + ":" + ((UnicastSender)res.ResultSender).EndPoint);
      Console.WriteLine(res.Result);
      return (bool)res.Result;
    }
    catch (InvalidOperationException e) {
      Console.WriteLine(e.Message);
    }
    catch (Exception e) {
      Console.WriteLine(e.Message);
    }
    return false;
  }

  /// <summary>
  /// Asynchronous check buddy method, use when making asynchronous calls
  /// </summary>
  /// <param name="address">A brunet address representation</param>
  public void AsCheckBuddy(string address) {
    Channel q = new Channel();
    q.CloseAfterEnqueue();

    // Delegate function gets called when close event occurs
    q.CloseEvent += delegate(Object o, EventArgs eargs) {
      RpcResult res = (RpcResult)q.Dequeue();
      Console.WriteLine(_remEP + ":" + ((UnicastSender)res.ResultSender).EndPoint);
      Console.WriteLine(res.Result);
      UpdateAddress(address);
    };

    _brpc.Rpc.Invoke(_brpc.IPHandler.CreateUnicastSender(_remEP), q, "RpcIpopNode.CheckBuddy", address);
  }

  public virtual void UpdateAddress(string address) {
    Console.WriteLine("Returning Address {0}", address);
  }

  /// <summary>
  /// Main method, entry point for RpcDNSClient
  /// </summary>
  /// <param name="args"></param>
  public static void Main(string[] args) {
    RpcDNSClient client = new RpcDNSClient();
    client.Init();
    client.AsGetAddress();

    while (true) {
      Console.WriteLine("Enter address to check:");
      string address = Console.ReadLine();
      client.CheckBuddy(address);
      Console.WriteLine("Enter name and address to add separated by a space");
      string input = Console.ReadLine();
      string[] tmp = input.Split(' ');
      client.AddBuddy(tmp[0], tmp[1]);
    }
  }
}
