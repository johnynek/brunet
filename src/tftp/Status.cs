namespace Brunet.Tftp {

  /**
   * This class represents the Status of an existing Transfer.
   * It can be used to display the Status to a user or to log
   * the status.
   *
   * Status has an event which fires when the Status changes
   * and an event which fires when the transfer is complete.
   *
   * In general, if you do not create the Status object, you
   * should only access the properties and the events.
   *
   */
  public class Status {

    /**
     * @param req Request associated with this status
     * @param data the Stream that is being read or written
     */
    public Status(Request req, System.IO.Stream data) {

    }
    ///////////////
    /// Properties
    ////////////

    protected System.IO.Stream _stream;
    public System.IO.Stream Data { get { return _stream; } }

    protected Error _err;
    public Error TftpError {
      get { return _err; }
    }

    protected bool _is_finished;
    /**
     * This event is fired when we are sure we are finished, i.e. all
     * data has made it across
     *
     * In the case of a Read, this happens after the last data is
     * written into the Data Stream.
     * 
     * In the case of the Write this happens when
     * the Ack to the last Data is received.
     */
    public bool IsFinished {
      get { return _is_finished; }
    }

    protected System.DateTime _last_action_time;
    public System.DateTime LastActionTime { get { return _last_action_time; } }

    protected int _last_block_number;
    public int LastBlockNumber {
      get { return _last_block_number; }
    }


    protected AHPacket _pack;
    /**
     * If there is a timeout, we may need to resend the previous
     * packet.  We keep it for that reason
     */
    public AHPacket LastSentPacket {
      get { return _pack; }
    }

    protected int _pending_block;
    /**
     * This is a block that we begun writing or reading
     * from Disk.  There is at most one of these at a time.
     */
    public int PendingBlock {
      get { return _pending_block; }
    }

    protected object _state;
    /**
     * Holds state information for the Pending Transfer
     */
    public object PendingState {
      get { return _state; }
    }

    protected long _sizebytes;
    public long SizeBytes { get { return _sizebytes; } }

    protected long _transbytes;
    /**
     * This is the number of bytes which have been confirmed as
     * transfered (Acknowledged bytes)
     *
     * In the case of Writing, this is the number of Ack'ed bytes
     *
     * In the case of Reading, this is the number of bytes written to disk 
     */
    public long TransferedBytes {
      get { return _transbytes; }
    }

    protected Request _req;
    public Request Request { get { return _req; } }

    ////////////
    /// Events
    /////////

    /**
     * Anytime the properties change, this method is called,
     * useful if you want to print or log status changes
     */
    public event System.EventHandler StatusChangedEvent;

    /**
     * This is called when the transfer is finally finished
     */
    public event System.EventHandler FinishedEvent;

    // ///////
    // Methods.  These should only be used by Tftp.Agent
    // ///////


    /**
     * @param block the block we are have delt with
     * @param size_of_block the size.  Using only the Size, we know if this is the last
     * @return true if this is the last block
     */
    public bool CompleteBlock(int block, int size_of_block) {
      return false;
    }

    /**
     * If something goes wrong, set the Status to Error
     */
    public void SetError(Error err) {

    }

    public void SetPacket(AHPacket p) {

    }

    public void StartBlock(int block, object state) {

    }

  }

}
