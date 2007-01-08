namespace Brunet {


/**
 * Handles replies in the Request/Reply protocol.
 * @see ReqrepManager
 */
public interface IReplyHandler {

  /**
   * This method is called when a reply comes back (maybe
   * more than once).
   *
   * @param man the ReqrepManager managing this node
   * @param mid the identifier request/reply
   * @param prot the protocol of the reply
   * @param payload a Stream that contains the payload
   * @param packet the original AHPacket (so we can see the header)
   * @param statistics statistical information about the request
   * @param state the state that was given to SendRequest is returned here
   * @return true if we should continue listening for replies,
   *          false if we should ignore future replies.
   */
  bool HandleReply(ReqrepManager man, ReqrepManager.ReqrepType rt,
		   int mid,
		   string prot,
		   System.IO.MemoryStream payload, AHPacket packet,
		   ReqrepManager.Statistics statistics,
		   object state);
  /**
   * @param man  the ReqrepManager managing this node
   * @param message_number the number for this request/reply
   * @param err The type of Error that has occured.
   * @param state the state that was given to SendRequest is returned here
   */
  void HandleError(ReqrepManager man, int message_number,
		   ReqrepManager.ReqrepError err, object state);
}
	
}
