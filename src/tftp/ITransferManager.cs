namespace Brunet.Tftp {

  public interface ITransferManager {

    /**
     * Deny the request
     * @param req the Request we are denying
     * @param reason the Error to send to our peer
     */
    void Deny(Request req, Error reason);

    /**
     * Allow the request and write or read the data from the given
     * stream
     * @param req the request to allow
     * @param data the stream to read the file from
     */
    Status Allow(Request req, System.IO.Stream data);

  }

}
