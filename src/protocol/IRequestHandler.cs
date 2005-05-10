namespace Brunet {

/**
 * Requests that arrive at the ReqrepManager are handled
 * by objects that implement this interface.
 */
public interface IRequestHandler {

  /**
   * This method must cause SendResponse to be called "soon".
   * The response should come in a time scale that is short
   * compared to the request timeout time.
   *
   * @param man the ReqrepManager receiving this packet
   * @param rt The request type.  It may be Request or LossyRequest
   * @param req the object which allows the ReqrepManager to associate the reply with a response
   * @param prot the Protocol of the payload inside the request
   * @param payload the "true" payload of the packet (the request information is skipped in this)
   * @param packet the original packet.  Use this to look at the headers if you like
   */
  void HandleRequest(ReqrepManager man, ReqrepManager.ReqrepType rt,
		   object req,
		   AHPacket.Protocol prot,
		   System.IO.MemoryStream payload, AHPacket packet);
	
}
	
}
