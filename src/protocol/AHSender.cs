/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com> University of Florida

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

using System;
using Brunet.Util;
using System.Collections.Generic;
using System.Threading;

namespace Brunet {

/**
 * This represents the fixed length header of AH Packets.
 * It covers the data after the AH PType and before the payload
 * PType.
 */
public class AHHeader : ICopyable {
  
  public static readonly int LENGTH = 46;
  protected readonly MemBlock _data;
  
  public readonly short Hops;
  public readonly short Ttl;
  public readonly ushort Opts;

  protected Address _dest;
  public Address Destination {
    get {
      if( null == _dest ) {
        _dest = AddressParser.Parse(_data.Slice(24,20));
      }
      return _dest;
    }
  }
  protected Address _src;
  public Address Source {
    get {
      if( null == _src ) {
        _src = AddressParser.Parse(_data.Slice(4,20));
      }
      return _src;
    }
  }

  protected AHHeader(short hops, AHHeader head) {
    //Set a new number of hops:
    Hops = hops;
    //Copy the rest:
    Ttl = head.Ttl;
    Opts = head.Opts;
    _src = head._src; 
    _dest = head._dest; 
    _data = head._data;
  }
  /** Parse the first LENGTH bytes to get the AHHeader
   */
  public AHHeader(MemBlock mb) {
    Hops = NumberSerializer.ReadShort(mb, 0);
    Ttl = NumberSerializer.ReadShort(mb, 2);
    //We parse the Address objects lazily
    Opts = (ushort)NumberSerializer.ReadShort(mb, 44);
    if( mb.Length != LENGTH ) {
      mb = mb.Slice(0,LENGTH);
    }
    _data = mb;
  }

  public AHHeader(short hops, short ttl, Address source, Address dest, ushort options) {
    //Make the header part:
    byte[] header = new byte[ LENGTH ];
    int offset = 0;
    //Write hops:
    NumberSerializer.WriteShort(hops, header, offset);
    Hops = hops;
    offset += 2;

    NumberSerializer.WriteShort(ttl, header, offset);
    Ttl = ttl;
    offset += 2;
    
    _src = source;
    offset += source.CopyTo(header, offset);

    _dest = dest;
    offset += dest.CopyTo(header, offset);

    Opts = options;
    NumberSerializer.WriteShort((short)options, header, offset);
    offset += 2;

    _data = MemBlock.Reference(header, 0, offset);
  }

  public int CopyTo(byte[] dest, int off) {
    _data.CopyTo(dest, off);
    //The Hops is not neccesarily correct in _data
    NumberSerializer.WriteShort(Hops, dest, off);
    return LENGTH;
  }

  public override int GetHashCode() {
    return Destination.GetHashCode();
  }

  public override bool Equals(object o) {
    AHHeader o_head = o as AHHeader;
    if( o_head != null ) {
      return (this.Hops == o_head.Hops) &&
             (this.Ttl == o_head.Ttl) &&
             (this.Opts == o_head.Opts) &&
             (this.Destination.Equals(o_head.Destination)) &&
             (this.Source.Equals(o_head.Source));
    }
    else {
      return false;
    }
  }

  public int Length { get { return LENGTH; } }

  /** return a new AHHeader with one more hop
   */
  public AHHeader IncrementHops() {
    short new_hops = (short)(Hops + 1);
    return new AHHeader(new_hops, this);
  }

  /** Deals with the options field in AHHeader objects
   */
  public class Options {
    protected static Dictionary<string, ushort> _string_to_ushort = new Dictionary<string, ushort>();
    protected static Dictionary<ushort, string> _ushort_to_string = new Dictionary<ushort, string>();      
  
    static Options() {
      _string_to_ushort["greedy"] = Greedy;
      _string_to_ushort["exact"] = Exact;
      _string_to_ushort["path"] = Path;
      _string_to_ushort["last"] = Last;
      _string_to_ushort["default"] = AddClassDefault;
      _string_to_ushort["annealing"] = Annealing;
  
      _ushort_to_string[Greedy] = "greedy";
      _ushort_to_string[Exact] = "exact";
      _ushort_to_string[Path] = "path";
      _ushort_to_string[Last] = "last";
      _ushort_to_string[AddClassDefault] = "default";
      _ushort_to_string[Annealing] = "annealing";
    }
    //These are delivery options controlling when the packet is delivered locally
    public static readonly ushort AddClassDefault = 0;
    /**
     * Only the very last node to see the packet gets it delivered in this
     * case.  It may be when TTL==HOPs, or it my be the last in some route.
     * This mode assumes the Greedy routing mode for structured addresses
     */
    public static readonly ushort Last = 1;
    /**
     * This mode assumes the Annealing routing mode for structured addresses
     */
    public static readonly ushort Path = 2;
    /**
     * This uses the greedy routing algorithm.  The packet always
     * gets closer to the destination until it can get no closer,
     * as is delivered to that node.
     */
    public static readonly ushort Greedy = 3;
    /**
     * This mode allows the packet to "go uphill" for one step.
     * However, every local minimum will get the packet.  So,
     * more than one node may receive the packet.
     * This mode is slightly fault tolerant.
     */
    public static readonly ushort Annealing = 4;
    /**
     * Only a node with an address that exactly matches the destination should
     * get the packet
     */
    public static readonly ushort Exact = 5;
    
    public static ushort StringToUShort(string mode) {
      if (_string_to_ushort.ContainsKey(mode)) {
        return _string_to_ushort[mode];
      }
      throw new SenderFactoryException("Unknown sender mode: " + mode);
    }
    
    public static string UShortToString(ushort mode) {
      if (_ushort_to_string.ContainsKey(mode)) {
        return _ushort_to_string[mode];
      }
      throw new SenderFactoryException("Unknown sender mode: " + mode);
    }
  }
}

public class AHSender : ISender {
  static AHSender() {
    SenderFactory.Register("ah", CreateInstance);
    _buf_alloc = new BufferAllocator(System.UInt16.MaxValue);
  }

  protected static AHSender CreateInstance(Node n, string uri) {
    string ahscheme; //Should be "ah"
    IDictionary<string, string> kvpairs = SenderFactory.DecodeUri(uri, out ahscheme);
    Address target = AddressParser.Parse(kvpairs["dest"]);
    ushort option = AHHeader.Options.StringToUShort(kvpairs["mode"]);
    return new AHSender(n, target, option);
  }

  protected Node _n;
  protected readonly Address _dest;
  public Address Destination { get { return _dest; } }
  protected readonly Address _source;
  public Address Source { get { return _source; } }
  protected short _hops;
  public short Hops { get { return _hops; } }
  protected short _ttl;
  public short Ttl { get { return _ttl; } }
  protected ushort _options;
  public ushort Options { get { return _options; } }

  private static BufferAllocator _buf_alloc;

  protected ISender _from;
  /*
   * Every packet comes from somewhere, it is either locally generated,
   * or it came from an edge.  This ISender sends "back" from where the
   * packet came from
   *
   * If this a local packet, it was Received from the local node
   */
  public ISender ReceivedFrom { get { return _from; } }
  //This is the serialized header:
  protected MemBlock _header;
  protected int _header_length;

  public AHSender(Node n, Address destination, ushort options)
  : this( n, n, destination, n.DefaultTTLFor(destination), options) {

  }

  public AHSender(Node n, Address destination, short ttl, ushort options)
    : this(n, n, destination, ttl, options) {

  }
  public AHSender(Node n, ISender from, Address destination, short ttl, ushort options) {
    _n = n;
    _from = from;
    //Here are the fields in the order they appear:
    _hops = 0;
    _ttl = ttl;
    _source = n.Address;
    _dest = destination;
    _options = options;
  }
  /**
   * This is probably the most commonly used AHSender
   */
  public AHSender(Node n, Address destination)
    : this(n, destination, n.DefaultTTLFor(destination),
           AHHeader.Options.AddClassDefault) {
    
  }

  override public bool Equals(object o) {
    AHSender ahs = o as AHSender;
    bool eq = false;
    if( ahs != null ) {
      eq = ahs.Destination.Equals( _dest );
      eq &= ( ahs._options == _options );
    }
    return eq;
  }

  override public int GetHashCode() {
    return _dest.GetHashCode();
  }

  public void Send(ICopyable data) {
    /*
     * Assemble an AHPacket:
     */
    if( _header == null ) {
      AHHeader ahh = new AHHeader(_hops, _ttl, _source, _dest, _options);
      _header = MemBlock.Copy(new CopyList( PType.Protocol.AH, ahh));
      _header_length = _header.Length;
    }
    byte[] ah_packet;
    int packet_length;
    int packet_offset;

    //Try to get the shared BufferAllocator, useful when
    //we don't know how big the data is, which in general
    //is just as expensive as doing a CopyTo...
    BufferAllocator ba = Interlocked.Exchange<BufferAllocator>(ref _buf_alloc, null);
    if( ba != null ) {
      try {
        ah_packet = ba.Buffer;
        packet_offset = ba.Offset;
        int tmp_off = packet_offset;
        tmp_off += _header.CopyTo(ah_packet, packet_offset);
        tmp_off += data.CopyTo(ah_packet, tmp_off);
        packet_length = tmp_off - packet_offset;
        ba.AdvanceBuffer(packet_length);
      }
      catch(System.Exception x) {
        throw new SendException(false, "could not write the packet, is it too big?", x);
      }
      finally {
        //Put the BA back
        Interlocked.Exchange<BufferAllocator>(ref _buf_alloc, ba);
      }
    }
    else {
      //Oh well, someone else is using the buffer, just go ahead
      //and allocate new memory:
      packet_offset = 0;
      packet_length = _header_length + data.Length;
      ah_packet = new byte[ packet_length ];
      int off_to_data = _header.CopyTo(ah_packet, 0);
      data.CopyTo(ah_packet, off_to_data);
    }
    MemBlock mb_packet = MemBlock.Reference(ah_packet, packet_offset, packet_length);
    /*
     * Now we announce this packet, the AHHandler will
     * handle routing it for us
     */
    _n.HandleData(mb_packet, _from, this);
  }

  public override string ToString() {
    return System.String.Format("AHSender(dest={0})",_dest);
  }

  /**
   * Converts the sender into a URI representation.
   * @returns URI for the sender.
   */
  public string ToUri() {
    return System.String.Format("sender:ah?dest={0}&mode={1}",
                                _dest.ToMemBlock().ToBase32String(),
                                AHHeader.Options.UShortToString(_options));
  }

}

/**
 * Send a message which should only be received by a node
 * which exactly matches the target address
 */
public class AHExactSender : AHSender {
  public AHExactSender(Node n, Address target)
    : base(n, target, n.DefaultTTLFor(target), AHHeader.Options.Exact) { }
}

/**
 * Send a message which should only be received by a node
 * which is closest to the target address
 */
public class AHGreedySender : AHSender {
  public AHGreedySender(Node n, Address target)
    : base(n, target, n.DefaultTTLFor(target), AHHeader.Options.Greedy) { }
}

public abstract class AHRoutingAlgorithm {

  /** compute the next step in the route
   * @param from the Edge the packet came in on
   * @param head the header to route
   * @return next connection and bool indicating if local node should get
   * packet.  If there is no next connection, the connection will be null
   */
  public abstract Pair<Connection, bool> NextConnection(Edge from, AHHeader head);
}

public class GreedyRouting : AHRoutingAlgorithm {
  protected readonly ConnectionList _structs;
  protected readonly Connection _local_con;
  protected readonly Pair<Connection, bool> _LOCAL;
  protected readonly Pair<Connection, bool> _NO_ONE;

  public GreedyRouting(AHAddress local, ConnectionList structured_cons) {
    //Fake connection to ourselves:
    _local_con = new Connection(null, local, "structured.self", null, null);
    int local_idx;
    _structs = ConnectionList.InsertInto(structured_cons, _local_con, out local_idx);
    _NO_ONE = new Pair<Connection, bool>(null, false);
    _LOCAL = new Pair<Connection, bool>(null, true);
  }

  public override Pair<Connection, bool> NextConnection(Edge from, AHHeader head) {
    Address dest = head.Destination;
    int d_idx = _structs.IndexOf(dest);
    Connection next_con;
    if( d_idx >= 0 ) {
      //We have a direct connection:
      next_con = _structs[d_idx];
    }
    else {
      //We have to check the right and the left:
      var ah_dest = (AHAddress)dest;
      int left_idx = ~d_idx;
      Connection l_c = _structs[left_idx];
      int right_idx = left_idx - 1;
      Connection r_c = _structs[right_idx];

      if( ah_dest.IsCloserToFirst((AHAddress)l_c.Address, (AHAddress)r_c.Address) ) {
        next_con = l_c;
      }
      else {
        next_con = r_c;
      }
      /*
       * Note, DO NOT DO COMPARISONS WITH INDEX VALUES!!!!!
       * do the the wrap around, _structs[x] == _structs[y]
       * when x = y + k * _structs.Count for all k, so equality
       * of Connection is not the same as equality of index
       */
      if( head.Hops >= head.Ttl ) {
        //Only deliver if we are the closest or last mode:
        bool local = next_con == _local_con || head.Opts == AHHeader.Options.Last;
        if( local ) {
          return _LOCAL;
        }
        else {
          return _NO_ONE;
        }
      }
    }
    return next_con == _local_con ? _LOCAL : new Pair<Connection, bool>(next_con, false);
  }
}
public class AnnealingRouting : AHRoutingAlgorithm {
  protected readonly ConnectionList _structs;
  protected readonly Connection _local_con;

  public AnnealingRouting(AHAddress local, ConnectionList structured_cons) {
    //Fake connection to ourselves:
    _local_con = new Connection(null, local, "structured.self", null, null);
    int local_idx;
    _structs = ConnectionList.InsertInto(structured_cons, _local_con, out local_idx);
  }

  public override Pair<Connection, bool> NextConnection(Edge from, AHHeader head) {
    Address dest = head.Destination;
    int d_idx = _structs.IndexOf(dest);
    if( d_idx >= 0 ) {
      //We have a direct connection:
      Connection next = _structs[d_idx];
      if( next != _local_con ) {
        return new Pair<Connection,bool>(next, false);
      }
      else {
        return new Pair<Connection, bool>(null, true);
      }
    }
    else {
      var ah_dest = (AHAddress)dest;
      int left_idx = ~d_idx;
      Connection l_c = _structs[left_idx];
      int right_idx = left_idx - 1;
      Connection r_c = _structs[right_idx];

      Connection next_con;
      Connection other_con;
      if( ah_dest.IsCloserToFirst((AHAddress)l_c.Address, (AHAddress)r_c.Address) ) {
        next_con = l_c;
        other_con = r_c;
      }
      else {
        next_con = r_c;
        other_con = l_c;
      }
      //See if we need to get it:
      /* everyone gets "path" packets,
       * if it's exact, only the destination gets it, handled above
       * otherwise, only the two nodes on either side of the destination get
       * it
       */
      bool local = head.Opts == AHHeader.Options.Path ||
                   ((next_con == _local_con || other_con == _local_con)
                    && head.Opts != AHHeader.Options.Exact);

      Connection to_send;
      //Check not to send it back the way it came:
      if(from == null) {
        /*
         * This packet is from us, so just send it to the closest
         * node, other than us.
         */
        to_send = _local_con != next_con ? next_con : other_con;
      }
      else if(next_con.Edge == from) {
        //Don't send it back the way it came:
        to_send = other_con;
      }
      else {
        //Great, the closest has not yet been visited:
        to_send = next_con;
      }
      //Now, make sure not to send it to ourselves:
      to_send = to_send == _local_con ? null : to_send;
      return new Pair<Connection, bool>(to_send, local);
    }
  }
}
public class DirectionalRouting : AHRoutingAlgorithm {
  protected readonly Pair<Connection, bool> _LEFT_FALSE;
  protected readonly Pair<Connection, bool> _RIGHT_FALSE;
  protected readonly Pair<Connection, bool> _LEFT_TRUE;
  protected readonly Pair<Connection, bool> _RIGHT_TRUE;
  protected readonly Pair<Connection, bool> _NULL_TRUE;
  protected readonly Pair<Connection, bool> _NULL_FALSE;


  public DirectionalRouting(AHAddress local, ConnectionList structs) {
    _NULL_TRUE = new Pair<Connection, bool>(null, true);
    _NULL_FALSE = new Pair<Connection, bool>(null, false);
    Connection left_c = null;
    Connection right_c = null;
    if( structs.Count > 0) {
      int local_idx = ~structs.IndexOf(local);
      int left_idx = local_idx;
      int right_idx = left_idx - 1;
      left_c = structs[left_idx];
      right_c = structs[right_idx];
    }
    _LEFT_FALSE = new Pair<Connection, bool>(left_c, false);
    _RIGHT_FALSE = new Pair<Connection, bool>(right_c, false);
    _LEFT_TRUE = new Pair<Connection, bool>(left_c, true);
    _RIGHT_TRUE = new Pair<Connection, bool>(right_c, true);
  }
  
  public override Pair<Connection, bool> NextConnection(Edge from, AHHeader h) {
    DirectionalAddress dest = (DirectionalAddress)h.Destination;
    if( h.Ttl >= h.Hops ) {
      //Deliver it to us but stop it here:
      return _NULL_TRUE;
    }
    if ( dest.Bearing == DirectionalAddress.Direction.Left ) {
      if( h.Opts == AHHeader.Options.Path ) {
        return _LEFT_TRUE;    
      }
      else {
        return _LEFT_FALSE;
      }
    }
    else if (dest.Bearing == DirectionalAddress.Direction.Right) {
      if( h.Opts == AHHeader.Options.Path ) {
        return _RIGHT_TRUE;    
      }
      else {
        return _RIGHT_FALSE;
      }
    }
    else {
      throw new System.Exception(
          System.String.Format("Unrecognized direction: {0}", dest.Bearing));
    }
  }
}

/**
 * This handles AHPackets which arrive at the node
 */
public class AHHandler : IDataHandler {

  protected readonly Node _n;
  protected AHState _state;

  protected class AHState {
    public readonly ConnectionList Leafs;
    public readonly ConnectionList Structs;
    public readonly AHAddress Local;

    protected readonly GreedyRouting _greedy;
    protected readonly AnnealingRouting _annealing;
    protected readonly DirectionalRouting _directional;

    protected AHState(AHState old_state, ConnectionList leafs) {
      Leafs = leafs;
      Structs = old_state.Structs;
      Local = old_state.Local;

      _directional = old_state._directional;
      _greedy = old_state._greedy;
      _annealing = old_state._annealing;
    }
   
    public AHState(AHAddress local, ConnectionList structs, ConnectionList leafs) {
      Leafs = leafs;
      Structs = structs;
      Local = local;
      
      _directional = new DirectionalRouting(local, structs);
      _greedy = new GreedyRouting(local, structs);
      _annealing = new AnnealingRouting(local, structs);
    }

    public AHRoutingAlgorithm GetRoutingAlgo(AHHeader head) {
      int addclass = head.Destination.Class;
      if( addclass == 0 ) {
        ushort opts = head.Opts;
        if( opts == AHHeader.Options.Last
         || opts == AHHeader.Options.Greedy ) {
          return _greedy;
        }
        else {
          return _annealing;
        }
      }
      else if( addclass == 124 ) {
        return _directional;
      }
      else {
        throw new Exception(
        String.Format("No router for class: {0}", addclass));
      }
    }

    public AHState UpdateLeafs(ConnectionList l) {
      return new AHState(this, l);
    }

    public AHState UpdateStructs(ConnectionList s) {
      return new AHState(Local, s, this.Leafs);
    }
  }

  /**
   * You still need to Subscribe this.  This constructor DOES NOT
   * do that
   */
  public AHHandler(Node n) {
    _n = n;
    var stcons = _n.ConnectionTable.GetConnections(ConnectionType.Structured);
    var lfcons = _n.ConnectionTable.GetConnections(ConnectionType.Leaf);
    _state = new AHState((AHAddress)n.Address, stcons, lfcons);
    _n.ConnectionTable.ConnectionEvent += this.ResetState;
    _n.ConnectionTable.DisconnectionEvent += this.ResetState;
  }
  protected void ResetState(object contab, EventArgs arg) {
    ConnectionEventArgs cea = (ConnectionEventArgs)arg;
    ConnectionList cel = cea.CList;
    //Here's the tranaction:
    AHState state = _state; //Only read of _state
    AHState old_state;
    AHState new_state;
    do {
      old_state = state;
      if( cel.MainType == ConnectionType.Structured ) {
        new_state = old_state.UpdateStructs(cel);
      }
      else if( cel.MainType == ConnectionType.Leaf ) {
        new_state =  old_state.UpdateLeafs(cel);
      }
      else {
        //we ignore this type:
        new_state = old_state;
      }
      state = System.Threading.Interlocked.CompareExchange<AHState>(ref _state, new_state, old_state);
    }
    while(old_state != state);
  }
  /**
   * Here we handle routing AHPackets
   */
  public void HandleData(MemBlock data, ISender ret_path, object st) {
    AHState state = _state; //Read the state, it can't change after the read
    var header = new AHHeader(data);
    var payload = data.Slice(header.Length);

    Connection next_con;
    //Check to see if we can use a Leaf connection:
    int dest_idx = state.Leafs.IndexOf(header.Destination);
    if( dest_idx >= 0 ) {
      next_con = state.Leafs[dest_idx];
    }
    else {
      var alg = state.GetRoutingAlgo(header);
      Pair<Connection, bool> result = alg.NextConnection(ret_path as Edge, header);
      if( result.Second ) {
        //Send a response exactly back to the node that sent to us
        var resp_send = new AHSender(_n, ret_path, header.Source,
                                       _n.DefaultTTLFor(header.Source),
                                       AHHeader.Options.Exact);
        _n.HandleData( payload, resp_send, this); 
      }
      next_con = result.First;
    }
    //Send it on:
    if( next_con != null ) {
      //Now we do the sending:
      var new_packet = new CopyList(PType.Protocol.AH,
                                    header.IncrementHops(),
                                    payload);
      try {
        next_con.Edge.Send(new_packet);
      }
      catch(EdgeException) {
        //Just drop the packet...
      }
    }
  }
}

}
