namespace Brunet.Tftp {

  /**
   * Represents the TFTP request.
   */
  public class Request {

    /**
     * The default constructor that creates a request with the standard
     * file transfer options
     */
    public Request(short local_tid, short peer_tid, Opcode req_type, Brunet.Address peer,
                   string filename) {

    }
    public Request(short local_tid, short peer_tid, Opcode req_type, Brunet.Address peer,
                   string filename, long tsize) {

    }


    /**
     * @param peer_tid the TID that our Peer is using
     * @param local_tid the TID that we use for this Request
     * @param peer the peer Node in this transfer
     * @param read_from the stream to read the data from
     */
    public Request(short peer_tid, short local_tid, Opcode req_type, Brunet.Address peer,
                   System.IO.Stream read_from) {

    }

    /**
     * Holds all the options for the request packet
     */
    protected System.Collections.Hashtable _options;

    /////////////////////////////////
    /// Properties
    ///////////////////////////////

    protected int _blksize;
    public int Blksize {
      get { return _blksize; }
    }

    protected string _filename;
    public string Filename {
      get { return _filename; }
    }

    protected bool _is_local;
    /**
     * If we created this request, IsLocal is true,
     * otherwise our Peer created it, and IsLocal is false
     */
    public bool IsLocal {
      get { return _is_local; }
    }

    protected short _localtid;
    /**
     * When we send packets this should always be the Source TID
     */
    public short LocalTID {
      get {
        return _localtid;
      }
    }

    protected string _mode;
    public string Mode {
      get { return _mode; }
    }

    protected Brunet.Address _peer;
    public Brunet.Address Peer { get { return _peer; } }

    protected short _peer_tid;
    /**
     * When we send packets this should always be the Destination TID
     *
     * This is the only property with a setter (because we don't know
     * the PeerTID when we create a request)
     */
    public short PeerTID {
      get {
        return _peer_tid;
      }
      set {
        _peer_tid = value;
      }
    }

    protected Opcode _reqtype;
    public Opcode Reqtype {
      get { return _reqtype; }
    }

    protected System.TimeSpan _timeout;
    public System.TimeSpan Timeout {
      get { return _timeout; }
    }

    protected long _tsize;
    public long Tsize {
      get { return _tsize; }
    }
    //////////////////////////////
    ///  Methods
    /////////////////////////////

    /**
     * Returns the string of the given option
     */
    public string GetOption(string name) {
      return "";
    }

    /**
     * @return true if the give option exists
     */
    public bool HasOption(string name) {
      return false;
    }

  }

}
