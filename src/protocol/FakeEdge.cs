/*
 * Dependencies : 
 Brunet.TransportAddress;
 Brunet.Edge;
 */

using Brunet;
using System;

namespace Brunet
{

  /**
   * This a derived class from the base class Edge.
   * It is designed for the sole purpose of testing the AHRoutingTable
   * The only functionality is that it has the local and remote TransportAddress
   */

  public class FakeEdge:Brunet.Edge
  {

    private TransportAddress local_add;
    private TransportAddress remote_add;

    public FakeEdge(TransportAddress local, TransportAddress remote)
    {
      local_add = local;
      remote_add = remote;
    }

    public override void Close()
    {
    }

    public override Brunet.TransportAddress LocalTA
    {
      get
      {
        return local_add;
      }
    }
    public override Brunet.TransportAddress RemoteTA
    {
      get
      {
        return remote_add;
      }
    }

    public override Brunet.TransportAddress.TAType TAType
    {
      get
      {
        return Brunet.TransportAddress.TAType.Tcp;
      }
    }
    public override DateTime LastOutPacketDateTime {
      get { return DateTime.Now; }
    }
    /**
     * @param p a Packet to send to the host on the other
     * side of the Edge.
     */
    public override void Send(Brunet.Packet p)
    {
    }

    public override bool IsClosed
    {
      get
      {
        return false;
      }
    }
    /**
     * @return true if the edge is an in-degree
     */
    public override bool IsInbound
    {
      get
      {
        return false;
      }
    }

  }

}
