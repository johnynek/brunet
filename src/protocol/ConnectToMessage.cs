/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>  University of Florida

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

using System.Collections;
using System.Collections.Specialized;
#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet
{

  /**
   * The ConnectionMessage that is sent out on the network
   * to request connections be made to the sender.
   *
   * When a Node sends out a ConnectToMessage, it puts
   * itself as the target.  This is because that node
   * is requesting that the recipient of the ConnectToMessage
   * connect to the sender (thus the sender is the target).
   *
   * When a node recieves a ConnectToMessage, the CtmRequestHandler
   * processes the message.  ConnectToMessages are sent by
   * Connector objects.
   *
   * This object is immutable
   * 
   * @see CtmRequestHandler
   * @see Connector
   */
  public class ConnectToMessage
  {

    /**
     * @param t connection type
     * @param target the Address of the target node
     */
    public ConnectToMessage(ConnectionType t, NodeInfo target)
    {
      _ct = Connection.ConnectionTypeToString(t);
      _target_ni = target;
      _neighbors = new NodeInfo[0]; //Make sure this isn't null
    }
    public ConnectToMessage(string contype, NodeInfo target)
    {
      _ct = contype;
      _target_ni = target;
      _neighbors = new NodeInfo[0]; //Make sure this isn't null
    }
    public ConnectToMessage(string contype, NodeInfo target, NodeInfo[] neighbors)
    {
      _ct = contype;
      _target_ni = target;
      _neighbors = neighbors;
    }

    public ConnectToMessage(IDictionary ht) {
      _ct = (string)ht["type"];
      _target_ni = NodeInfo.CreateInstance((IDictionary)ht["target"]);
      IList neighht = ht["neighbors"] as IList;
      if( neighht != null ) {
        _neighbors = new NodeInfo[ neighht.Count ];
        for(int i = 0; i < neighht.Count; i++) {
          _neighbors[i] = NodeInfo.CreateInstance( (IDictionary)neighht[i] );
        }
      }
    }

    protected string _ct;
    public string ConnectionType { get { return _ct; } }

    protected NodeInfo _target_ni;
    public NodeInfo Target {
      get { return _target_ni; }
    }
    protected NodeInfo[] _neighbors;
    public NodeInfo[] Neighbors { get { return _neighbors; } }
    
    public override bool Equals(object o)
    {
      ConnectToMessage co = o as ConnectToMessage;
      if( co != null ) {
        bool same = true;
	same &= co.ConnectionType == _ct;
	same &= co.Target.Equals( _target_ni );
	if( _neighbors == null ) {
          same &= co.Neighbors == null;
	}
	else {
          int n_count = co.Neighbors.Length;
	  for(int i = 0; i < n_count; i++) {
            same &= co.Neighbors[i].Equals( Neighbors[i] );
	  } 
	}
	return same;
      }
      else {
        return false;
      }
    }
    override public int GetHashCode() {
      return Target.GetHashCode();
    }

    public IDictionary ToDictionary() {
      ListDictionary ht = new ListDictionary();
      ht["type"] = _ct;
      ht["target"] = _target_ni.ToDictionary();
      ArrayList neighs = new ArrayList(Neighbors.Length);
      foreach(NodeInfo ni in Neighbors) {
        neighs.Add( ni.ToDictionary() );
      }
      ht["neighbors"] = neighs;
      return ht;
    }
    
  }
//Here are some Unit tests:
#if BRUNET_NUNIT
//Here are some NUnit 2 test fixtures
  [TestFixture]
  public class ConnectToMessageTester {

    public ConnectToMessageTester() { }
    
    public void HTRoundTrip(ConnectToMessage ctm) {
      ConnectToMessage ctm2 = new ConnectToMessage( ctm.ToDictionary() );
      Assert.AreEqual(ctm, ctm2, "CTM HT Roundtrip");
    }
    [Test]
    public void CTMSerializationTest()
    {
      Address a = new DirectionalAddress(DirectionalAddress.Direction.Left);
      TransportAddress ta = TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:5000"); 
      NodeInfo ni = NodeInfo.CreateInstance(a, ta);
      ConnectToMessage ctm1 = new ConnectToMessage(ConnectionType.Unstructured, ni);
      
      HTRoundTrip(ctm1);

      //Test multiple tas:
      ArrayList tas = new ArrayList();
      tas.Add(ta);
      for(int i = 5001; i < 5010; i++)
        tas.Add(TransportAddressFactory.CreateInstance("brunet.tcp://127.0.0.1:" + i.ToString()));
      NodeInfo ni2 = NodeInfo.CreateInstance(a, tas);

      ConnectToMessage ctm2 = new ConnectToMessage(ConnectionType.Structured, ni2);
      HTRoundTrip(ctm2);
      //Here is a ConnectTo message with a neighbor list:
      NodeInfo[] neighs = new NodeInfo[5];
      for(int i = 0; i < 5; i++) {
	string ta_tmp = "brunet.tcp://127.0.0.1:" + (i+80).ToString();
        NodeInfo tmp =
		NodeInfo.CreateInstance(new DirectionalAddress(DirectionalAddress.Direction.Left),
	                     TransportAddressFactory.CreateInstance(ta_tmp)
			    );
	neighs[i] = tmp;
      }
      ConnectToMessage ctm3 = new ConnectToMessage("structured", ni, neighs);
      HTRoundTrip(ctm3);
#if false
      Console.Error.WriteLine( ctm3.ToString() );
      foreach(NodeInfo tni in ctm3a.Neighbors) {
        Console.Error.WriteLine(tni.ToString());
      }
#endif
    }
  }

#endif

}
