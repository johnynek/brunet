/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

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
