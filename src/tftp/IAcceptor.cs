
namespace Brunet.Tftp {

/**
 * This interface represents the permission system.  When
 * the Agent gets a request, it asks its Acceptor to accept it
 */
public interface IAcceptor {
  /**
   * @param req the Request to allow or deny
   * @param tm the TransferManager to give the decision to (Allow or Deny)
   */
  void Accept(Request req, ITransferManager tm);
}
	
}
