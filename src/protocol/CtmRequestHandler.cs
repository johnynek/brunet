/*
 * Dependencies : 
 * Brunet.ConnectionPacket
 * Brunet.ConnectionMessage
 * Brunet.ConnectionMessageParser
 * Brunet.ConnectionType
 * Brunet.ConnectToMessage
 * Brunet.Edge
 * Brunet.IAHPacketHandler
 * Brunet.Linker
 * Brunet.Node
 * Brunet.AHPacket
 * Brunet.Packet
 * Brunet.PacketForwarder
 * Brunet.TransportAddress
 */

using System;
using System.Collections;
//using log4net;

namespace Brunet
{

/**
 * When a ConnectToMessage *REQUEST* comes in, this object handles it.
 * Each node will have its own CtmRequestHandler.
 * When the CtmRequestHandler gets a ConnectToMessage, it creates a Linker
 * to link to the node that sent the message, and it sends a *RESPONSE*
 * ConnectToMessage back to the Node that made the request.
 *
 * The response will be handled by the Connector, which is the object which
 * initiates the Connection operations.
 *
 * @see Connector
 * @see Linker
 * @see Node
 * 
 */

  public class CtmRequestHandler:IAHPacketHandler
  {
  //private static readonly ILog _log = LogManager.GetLogger( typeof(CtmRequestHandler) );
    protected ConnectionMessageParser _cmp;
  /**
   */
    public CtmRequestHandler()
    {
      _cmp = new ConnectionMessageParser();
    }

  /**
   * This is used to connect to a remote node as a result
   * of a request
   * 
   * @param node the Node that received the request
   * @param p the packet received
   * @param from the Edge the request came from
   */
    public void HandleAHPacket(object node, AHPacket p, Edge from)
    {
     try {
      ConnectToMessage ctm = (ConnectToMessage)_cmp.Parse(p);
      Node n = (Node) node;
//stop now if we don't have a ConnectToMessage Request
      if (ctm.Dir == ConnectionMessage.Direction.Response) {
        return;
      }

      /*System.Console.WriteLine("Got CTM Request,"
		+ n.Address.ToString() + " connectTo: "
		+ ctm.TargetAddress.ToString() + " ConType: " + ctm.ConnectionType);*/

      /*_log.Info("Got CTM Request,"
		+ n.Address.ToString() + " connectTo: "
		+ ctm.TargetAddress.ToString());*/
      Linker l = new Linker(n);
      l.Link(ctm.TargetAddress, ctm.TransportAddresses, ctm.ConnectionType);
    /**
     * Send a response no matter what
     */
      ConnectToMessage local_response_ctm =
        new ConnectToMessage(ctm.ConnectionType,
                             n.Address,
			     n.LocalTAs);
      local_response_ctm.Id = ctm.Id;
      local_response_ctm.Dir = ConnectionMessage.Direction.Response;

//Send the response with a TTL 4 times larger than the number of hops
//it took to get here (just to give some headroom)
      short ttl;
      if (p.Hops < (AHPacket.MaxTtl / 4)) {
//This makes sure that the ttl is valid and never overflows : 
        ttl = (short) (4 * p.Hops + 1);
      }
      else {
        ttl = AHPacket.MaxTtl;
      }
      AHPacket response = new AHPacket(0, ttl,
                                       n.Address,
                                       ctm.TargetAddress,
                                       AHPacket.Protocol.Connection,
                                       local_response_ctm.ToByteArray());
      if (!p.Source.Equals(ctm.TargetAddress) &&
          !p.Source.Equals(n.Address) ) {
	//There is no point in forwarding through ourselves
//This was a forwarded packet, we must send a forwarded response :  
      //_log.Info("Sending a forwarded CTM Response");
        response =
          PacketForwarder.WrapPacket(p.Source, ttl, response);
      }
      //_log.Info("Sending CTM Response");
      n.Send(response);
     }
     catch(Exception x) {
     /*_log.Error("CtmRequestionHandler got exception on packet: " +
       p.ToString(), x); */
     } 
    }

    public bool HandlesAHProtocol(AHPacket.Protocol type)
    {
      return (type == AHPacket.Protocol.Connection);
    }
  }

}
