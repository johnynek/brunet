using System;
using System.Text;
using System.Collections;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Diagnostics; 
 
//[assembly:log4net.Config.DOMConfigurator(Watch = true)]
 
namespace Brunet
{
  public class AddtoDns
  {
 
    ///This tester simply establishes the Brunet network and log the edges made
 
    static void Main(string[] args)
    {

      String config_file = args[0];
      NetworkConfiguration network_configuration = NetworkConfiguration.Deserialize(config_file);

      for(int i = 0; i < network_configuration.Nodes.Count; i++){

        NodeConfiguration this_node_configuration = (NodeConfiguration)network_configuration.Nodes[i];
        TransportAddressConfiguration local_ta_configuration = (TransportAddressConfiguration)this_node_configuration.TransportAddresses[0];
        short port = local_ta_configuration.Port;
        SHA1 sha = new SHA1CryptoServiceProvider();
        String local_ta = local_ta_configuration.GetTransportAddressURI();
        //We take the local transport address plus the port number to be hashed to obtain a random AHAddress
        byte[] hashedbytes = sha.ComputeHash(Encoding.UTF8.GetBytes(local_ta + port));
        //inforce type 0
        hashedbytes[Address.MemSize - 1] &= 0xFE;
        AHAddress _local_ahaddress = new AHAddress(hashedbytes);
        Console.WriteLine(_local_ahaddress.ToBigInteger().ToString() + ' ' + local_ta_configuration.Address + ' ' + (port%25000));
      }
    }

  }
}

