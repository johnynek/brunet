// Brunet.Address
// Brunet.StructuredAddress;
// Brunet.BigInteger;
// Brunet.AHAddress
// Brunet.NumberSerializer
using System;
using System.Collections;

namespace Brunet
{

  /**
   * Represents unicast addresses which have a one to
   * one relationship with Nodes in the Brunet system.
   * Each node has at most one AHAddress, and each
   * AHAddress has at most one Node associated with
   * it.
   */

  public class InitParameters
  {
    public AHAddress nodeaddress;
    public void NodeAddress(short instance)
    {
      byte[]address = new byte[Address.MemSize];
      for (int i = 0; i < address.Length; i++) {
        address[i] = (byte) 0;
      }
      /* Make sure we don't write into the last byte, since
       * all AHAddresses must be even
       */
      NumberSerializer.WriteShort(instance, address, 17);
      nodeaddress = new AHAddress(address);

    }
    public short nodeport;
    protected ArrayList node_remote_ta;
    public ArrayList NodeRemoteTA {
      get {
        return node_remote_ta;
      }
    }
    protected ArrayList node_remote_instances;
    public ArrayList NodeRemoteInstances {
      get {
        return node_remote_instances;
      }
    }
    public AHAddress RemoteAddress(short jj){
      byte[]address = new byte[Address.MemSize];
      for (int i = 0; i < address.Length; i++) {
        address[i] = (byte) 0;
      }
      /* Make sure we don't write into the last byte, since
       * all AHAddresses must be even
       */
      short inst = (short)(NodeRemoteInstances[jj]);
      NumberSerializer.WriteShort(inst, address, 17);
      return (new AHAddress(address));
    }
    public short remote_ta_number;
    public InitParameters(){
      NodeAddress(0);
      nodeport = 0;
      remote_ta_number=0;
      //node_remote_instances = ArrayList.Synchronized( new ArrayList() );
      //node_remote_ta =  ArrayList.Synchronized( new ArrayList() );
    }
  }
}
