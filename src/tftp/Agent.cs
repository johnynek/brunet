using Brunet;
using System;
using System.IO;
using System.Collections;

namespace Brunet.Tftp {

/**
 * Handles reading and writing files via TFTP over Brunet
 */
public class Agent : ITransferManager, IAHPacketHandler {
	
  public Agent(Node n, IAcceptor read_acceptor, IAcceptor write_acceptor) {
    _rand = new Random();
    /**
     * @todo Make a heartbeat handler which will use the Node HeartbeatEvent
     * to resend packets after the timeout
     */
  }

  /////////
  /// Properties
  ////////

  protected Node _node;
  public Node Node { get { return _node; } }

  protected IAcceptor _read_acc;
  public IAcceptor ReadAcceptor { get { return _read_acc; } }

  protected IAcceptor _write_acc;
  public IAcceptor WriteAcceptor { get { return _write_acc; } }
	
  /////////////
  /// Public Methods
  ////////////
 
  /**
   * Implementing the interface from ITransferManager
   * When a request is allowed, we set up the state and
   * start acting
   */
  public Status Allow(Request req, System.IO.Stream data) {
 
    Status stat = new Status(req, data);
    lock( _sync ) { 
      _tid_to_status[req.LocalTID] = stat; 
    }

    if( req.Reqtype == Opcode.ReadReq ) {
      /**
       * We are starting a read request here.  We send the
       * first data packet as an ack of this request
       */
      StreamState rs = new StreamState();
      int new_block = 1;
      stat.StartBlock(new_block, rs);
      rs.Stat = stat;
      rs.Block = new_block;
      rs.Data = new byte[ stat.Request.Blksize ];
      rs.Offset = 0;
      stat.Data.BeginRead(rs.Data, rs.Offset, stat.Request.Blksize,
			  new AsyncCallback(this.ReadHandler),
			  rs);
    }
    else if( req.Reqtype == Opcode.WriteReq ) {
      SendAck(req, 0);
    }
    else {
      /**
       * This can't happen because we would only process Read or Write
       * requests
       * @todo add error handling if ReqType is non-sensical
       */
    }
    return stat;
  }

  /**
   * Implementing the interface from ITransferManager
   */
  public void Deny(Request req, Error err) { 
    SendError(req, err);
    //Pop this request out of our memory:
    ReleaseTID(req.LocalTID);
  }
  
  /**
   * Get a file from a remote node and write it into the data stream
   */
  public Status Get(Address target, string filename, Stream data) {
    //First we make a TID for this transfer:
    short tid = GetNextTID();
    Request req;
    //Send the Length if we can:
    if( data.CanSeek ) {
      req = new Request(tid, DefaultTID, Opcode.ReadReq,
		                target, filename, data.Length);
    }
    else {
      req = new Request(tid, DefaultTID, Opcode.ReadReq,
		                target, filename);
    }
    SendRequest(req);
    Status stat = new Status(req, data);
    lock( _sync ) { 
      _tid_to_status[tid] = stat; 
    }
    return stat;
  }

  /**
   * @param tftpid the id of an existing transfer
   * @return the status of this id.  Returns null if it is an unknown id
   */
  public Status GetStatus(short tftpid) {
    lock( _sync ) {
      return (Status)_tid_to_status[tftpid];
    }
  }
 
  /**
   * Implementing from IAHPacketHandler
   * This is where we do the meat of the operating
   */
  public void HandleAHPacket(object node, AHPacket p, Edge from) {
    Node n = (Node)node;
    Stream packet_data = p.PayloadStream;
    short peer_tid = NumberSerializer.ReadShort(packet_data);
    short local_tid = NumberSerializer.ReadShort(packet_data);
    Opcode op = (Opcode)NumberSerializer.ReadShort(packet_data);
    /**
     * If there is any Error, an exception is thrown and the
     * Error is sent to the node which sent us this packet
     */
    Error err = null;
    //This is the Request to which this packet is associated
    Request req = null;
    try { 
      if( local_tid == DefaultTID ) {
	/**
	 * This is a new request being made to us from a remote node
	 */
        lock( _sync ) {
          //Check to see if this request is a duplicate:
          foreach(Request openr in _open_requests) {
            if( openr.Peer.Equals(p.Source) &&
	        (openr.PeerTID == peer_tid) ) {
	      req = openr;
            /**
	     * We need to send an Error response when we
	     * get the same TID from the same Address. 
	     * This must be a duplicate packet.
	     */
              err = new Error(Error.Code.NotDefined, "Duplicate Source TID");
	      throw new Exception();
	    }
          }
        }
        //This is a new operation
        local_tid = GetNextTID();
        //We need to parse the request and assign a new TID to this transfer
        req = new Request(peer_tid, local_tid, op, p.Source, packet_data);
        //If we get here, this is a new request
        IAcceptor acc = null;
        if( op == Opcode.ReadReq ) {
          acc = _read_acc;
        }
        else if( op == Opcode.WriteReq ) {
	  acc = _write_acc;
        }
      
        if( acc != null ) {
	  _open_requests.Add(req);
          acc.Accept(req, this);
        }
        else {
          //Something bad happened
	  err = new Error(Error.Code.IllegalOp, "Unknown Opcode");
	  throw new Exception();
        }
      }
      else if (_tid_to_status.ContainsKey(local_tid) ) {
       //This is continuing an existing transfer
       Status stat = (Status)_tid_to_status[local_tid];
       if( stat == null) {
	/**
	 * Status is null until a Request is accepted or denied.
	 * This request is either a (lucky) bug or an attack.
	 * You see, there is no way for the Peer to know which
	 * TID we are using when status is null.  This only happens
	 * when a TID has been assigned locally, but not yet sent
	 * over the network.  Hence, the Peer made a lucky guess.
	 */
	Error error = new Error(Error.Code.UnknownTID, "say what?");
        AHPacket resp = CreateErrorPacket(p.Source, DefaultTID, peer_tid, error);
        _node.Send(resp);
       }
       else {
       req = stat.Request;
       short block;
        switch(op) {
        case Opcode.Data:
	      ///@todo consider carefully that this works in both directions
	      ///@todo make all blocks ushort (so we can use an int == -1 to initialize)
              block = NumberSerializer.ReadShort(packet_data);
              /**
	       * When data comes, we send an Ack
	       */
	      lock( stat ) {
	        if( stat.LastBlockNumber == block ) {
	          /*
		   * This is a block we have already seen
		   */
	          SendAck(req, block);
		}
		else if( stat.PendingBlock == block ) {
                  //We are already dealing with this data
		  //So, we do nothing here
		}
		else {
	          /*
		   * This is a new Data block to us!
		   * Write it to disk and then send the Ack
		   */
		  StreamState ws = new StreamState();
	          stat.StartBlock(block, ws);
		  //This is new data we have not yet seen
		  ws.Stat = stat;
		  ws.Block = block;
		  ws.Data = new byte[ stat.Request.Blksize ];
		  ws.Offset = 0;
	          //The packet data is a MemoryStream which never blocks:
	          ws.Size = packet_data.Read(ws.Data, 0, stat.Request.Blksize);
	          stat.Data.BeginWrite(ws.Data, ws.Offset, ws.Size,
				       new AsyncCallback(this.WriteHandler),
				       ws);
		}
	      }
	      break;
        case Opcode.Ack:
              block = NumberSerializer.ReadShort(packet_data);
	      /**
	       * When an Ack comes, we know our last Data
	       * has made it across and we send the next Data
	       * We complete the block associated with that,
	       * and if we are not done, we start the next read
	       */
	      lock( stat ) {
	        StreamState sent = (StreamState)stat.PendingState;
	        bool finished = stat.CompleteBlock(block, sent.Size);
	        if( !finished ) {
                  //Do the next read:
		  StreamState rs = new StreamState();
		  int new_block = block + 1;
		  stat.StartBlock(new_block, rs);
		  rs.Stat = stat;
		  rs.Block = new_block;
		  rs.Data = new byte[ stat.Request.Blksize ];
		  rs.Offset = 0;
	          stat.Data.BeginRead(rs.Data, rs.Offset, stat.Request.Blksize,
				       new AsyncCallback(this.ReadHandler),
				       rs);
		}
	      }
	      break;
        case Opcode.Error:
              Error.Code code = (Error.Code)NumberSerializer.ReadShort(packet_data);
	      string message = ReadAsciiStringFrom(packet_data);
	      //Things did not work out for us
	      stat.SetError(new Error(code, message)); 
	      break;
        default:
	      err = new Error(Error.Code.IllegalOp, "Unknown Opcode");
	      throw new Exception();
        }//End of switch
       }//End of case where stat != null
      }//End of case where TID is known
      else {
        //Never heard of this TID!
	Error error = new Error(Error.Code.UnknownTID, "say what?");
        AHPacket resp = CreateErrorPacket(p.Source, DefaultTID, peer_tid, error);
        _node.Send(resp);
      }
    }
    catch(Exception x) {
      /**
       * If there has been any Exception or Error, we will wind up here
       * in which case we send an error response and release this TID.
       */
      if( err == null ) {
        //Set the error
	err = new Error(Error.Code.NotDefined, x.ToString() );
      }
      SendError(req, err);
      ReleaseTID(local_tid);
    }
  }
 
  /**
   * Implementing from IAHPacketHandler
   */
  public bool HandlesAHProtocol(AHPacket.Protocol type) {
    return (type == AHPacket.Protocol.Tftp);
  }
  /**
   * Write all the data from the current position in data until the
   * end of the stream
   */
  public Status Put(Stream data, Address target, string filename) {
    //First we make a TID for this transfer:
    short tid = GetNextTID();
    Request req;
    //Send the Length if we can:
    if( data.CanSeek ) {
      req = new Request(tid, DefaultTID, Opcode.WriteReq,
		                target, filename, data.Length);
    }
    else {
      req = new Request(tid, DefaultTID, Opcode.WriteReq,
		                target, filename);
    }
    SendRequest(req);
    Status stat = new Status(req, data);
    lock( _sync ) { 
      _tid_to_status[tid] = stat; 
    }
    return stat;
  }


  /**
   * Since TFTP uses null terminated strings in several
   * places, this function handles reading them
   * This code is called by several classes in this package
   */
  static public string ReadAsciiStringFrom(Stream from) {
    return "";
  }

  /**
   * A convienience function to write null-terminated strings into
   * streams
   */
  static public void WriteAsciiStringTo(string s, Stream to) {

  }
  
  /////////////
  /// Public Constants
  ///////
  /**
   * This is the TID to send to when initiating a connection according to RFC1350
   */
  public static readonly short DefaultTID = 69;
  
  /////////////////
  /// Protected Implemenation variables
  //////////////
  /**
   * Returns the Status for a given TID.  The Status is null
   * between the time that an incoming request arrives and it
   * is Accept ed or Deny ed
   */
  protected Hashtable _tid_to_status;
  /**
   * A list of the open Request objects.  We use this to
   * check for duplicated request packets
   */
  protected ArrayList _open_requests;
  protected Random _rand;
  protected object _sync;

  //Here is a protected Inner Class to deal with the Asynchronous stream operations:
  protected class StreamState {
    public Status Stat;
    public byte[] Data;
    public int Offset;
    public int Size;
    public int Block;
  }
  
  /////////////////
  /// Protected Methods
  /////////////////
  
  /**
   * Create the Data packet
   * @param tid Our TID for transfer we are working with
   */
  protected AHPacket CreateDataPacket(short tid, byte[] data, int offset, int size) {
    return null;
  }
 
  /**
   * Create an AHPacket representing an Error
   */
  protected AHPacket CreateErrorPacket(Address destination,
		                              short sourcetid,
		                              short desttid,
					      Error err) {
    byte[] body = new byte[1024];
    //Write the our TID
    NumberSerializer.WriteShort(sourcetid, body, 0);
    //Write the other TID
    NumberSerializer.WriteShort(desttid, body, 2);
    int size = 4;
    size += err.CopyTo(body, 4);
    //Send the error:
    AHPacket resp = new AHPacket(0, 32, _node.Address, destination,
		                 AHPacket.Protocol.Tftp, body, 0, size);
    return resp; 
  }
  
  /**
   * The returned value cannot be reused until
   * the method ReleaseTID is called
   * @return an unused TID
   */
  protected short GetNextTID() {
    short tid = 1;
    lock( _sync ) {
      do {
        tid = (short)_rand.Next( Int16.MinValue, Int16.MaxValue );
        //Avoid tid == DefaultTID as per RFC1350 
        if( tid == DefaultTID ) { tid = (short)(DefaultTID + 1); }
      } while( _tid_to_status.ContainsKey( tid ) );
      _tid_to_status[tid] = null;
    }
    return tid;
  }
 
  /**
   * Since we never block, we do asynchronous Stream reads.  When
   * the read returns, this method handles it
   */
  protected void ReadHandler(IAsyncResult ar) {
    StreamState rs = (StreamState)ar.AsyncState;
    try {
      int got = rs.Stat.Data.EndRead(ar);
      rs.Size = got;
      //Now this data has hit the disk, so we are okay
      AHPacket p = CreateDataPacket(rs.Stat.Request.LocalTID,
		                    rs.Data, rs.Offset, rs.Size);
      //Remember this packet in case we time out:
      rs.Stat.SetPacket(p);
      //Send it to our peer
      _node.Send(p);
    }
    catch(Exception x) {
      //Uh oh.  End the transfer and send an error to our Peer
      Error err = new Error(Error.Code.NotDefined, x.ToString());
      SendError(rs.Stat.Request, err);
      rs.Stat.SetError(err);
    }
  }
  
  /**
   * Return a given TID to the pool of availible TID numbers and
   * remove all data associated with this tid
   */
  protected void ReleaseTID(short tid) {
    Status s = null;
    lock( _sync ) {
      s = (Status)_tid_to_status[tid];
      _tid_to_status.Remove(tid);
      _open_requests.Remove(s.Request);
    }
  }
  
  /**
   * Send an acknowledgement for the given block in the given transfer
   */
  protected void SendAck(Request req, int block) {
    byte[] body = new byte[8];
    //Write the our TID
    NumberSerializer.WriteShort(req.LocalTID, body, 0);
    //Write the other TID
    NumberSerializer.WriteShort(req.PeerTID, body, 2);
    //Write the OP:
    NumberSerializer.WriteShort((short)Opcode.Ack, body, 4);
    //Write the Block number:
    NumberSerializer.WriteShort((short)block, body, 6);
    AHPacket resp = new AHPacket(0, 32, _node.Address, req.Peer, 
		                 AHPacket.Protocol.Tftp, body);
    _node.Send(resp);
  }


  /**
   * Send an error message for the transfer associated with Req
   */
  protected void SendError(Request req, Error err) {
    AHPacket resp = CreateErrorPacket(req.Peer, req.LocalTID, req.PeerTID, err);
    _node.Send(resp);
  }

  /**
   * Send this (Local) Request
   */
  protected void SendRequest(Request req) {
  
  }
  
  /**
   * Since we never block, we do asynchronous Stream writes.  When
   * the write returns, this method handles it
   */
  protected void WriteHandler(IAsyncResult ar) {
    StreamState ws = (StreamState)ar.AsyncState;
    try {
      ws.Stat.Data.EndWrite(ar);
      //Now this data has hit the disk, so we are okay
      SendAck(ws.Stat.Request, ws.Block);
      //Update the Status:
      bool finished = ws.Stat.CompleteBlock( ws.Block, ws.Size );
    }
    catch(Exception x) {
      //Uh oh.  End the transfer and send an error to our Peer
      Error err = new Error(Error.Code.NotDefined, x.ToString());
      SendError(ws.Stat.Request, err );
      ws.Stat.SetError(err);
    }
  }
}
	
}
