/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2010  P. Oscar Boykin <boykin@pobox.com>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using Brunet.Util;
using Brunet.Collections;
using SCG = System.Collections.Generic;
#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Messaging {

/*
 * Fragment into sending a ICopyable with a maximum length
 * Header: frag\0 | crc32 (4bytes) | id (4 bytes) | block (2 bytes)
 * So, total header: 5 + 4 + 4 + 2 = 15 bytes
 */
public class FragmentingSender : ISender, IWrappingSender {
// ///////////
// Static
// ///////////
  public static readonly PType FragPType = new PType("frag");
  /* Header: frag\0 | crc32 (4bytes) | id (4 bytes) | block (2 bytes)
   * So, total header: 5 + 4 + 4 + 2 = 15 bytes
   */
  private static readonly int HEADER_SIZE = FragPType.Length + 4 + 4 + 2;
  
  //2^15 - 1 maximum number of packets
  public static readonly ushort MAX_BLOCK_CNT = 0x7fff;
  public static readonly ushort LAST_BLOCK_FLAG = 0x8000;

  static FragmentingSender() {
    SenderFactory.Register("frag", CreateInstance);
  }

// //////////
// Member Variables
// //////////

  private readonly ISender _wrapped_sender;
  public ISender WrappedSender { get { return _wrapped_sender; } }
  
  public readonly int MAX_PAYLOAD_SIZE;
  ///This is the maximum size the FragmentingSender can handle:
  public readonly int MAX_SEND_SIZE;
 
  public FragmentingSender(int max_payload_size, ISender underlying) {
    _wrapped_sender = underlying;
    MAX_PAYLOAD_SIZE = max_payload_size;
    MAX_SEND_SIZE = MAX_PAYLOAD_SIZE * MAX_BLOCK_CNT;
  }
  public static ISender CreateInstance(object ctx, string uri) {
    string scheme;
    var kv = SenderFactory.DecodeUri(uri, out scheme);
    if( scheme.Equals("frag") ) {
      int max = System.Int32.Parse(kv["max"]);
      string wuri = kv["wrap"];
      ISender wrapped = SenderFactory.CreateInstance(ctx, wuri);
      return new FragmentingSender(max, wrapped);
    }
    else {
      throw new System.Exception("Unrecognized scheme: " + scheme);
    }
  }

  /*
   * Prefer evenly sized blocks to minimize chances of
   * further fragmentation by lower layers
   */
  protected int ComputeBlockSize(int datalength) {
    int max_size = MAX_PAYLOAD_SIZE - HEADER_SIZE;
    if( datalength % max_size != 0 ) {
      //Let's make evenly sized blocks:
      int block_cnt = (datalength / max_size) + 1;
      return datalength / block_cnt + 1; //Round up
    }
    else {
      //In this case, we can evenly divide:
      return max_size;
    }
  }
  public override bool Equals(object o) {
    var fso = o as FragmentingSender;
    return null != fso ? fso._wrapped_sender.Equals(_wrapped_sender) : false;
  }
  public override int GetHashCode() { return _wrapped_sender.GetHashCode(); }

  public void Send(ICopyable data) {
    //We always use the fragmenting header to signal
    //to the receiving end that we support (and want)
    //fragmentation.
    MemBlock rest = data as MemBlock;
    if(null == rest) {
      rest = MemBlock.Copy(data);
    }
    int len = rest.Length;
    if( len > MAX_SEND_SIZE ) {
      throw new SendException(true,
                 System.String.Format("Packet too large: {0}, MaxSize = {1}",len,
                 MAX_SEND_SIZE));
    }
    uint crc32 = rest.Read<uint>(Crc32.ComputeChecksum);
    //Avoid a RNG operation for now, might need to reconsider
    //If we use RNG, we have to touch state, that has thread-safety issues
    int id = _wrapped_sender.GetHashCode() ^ rest.GetHashCode();
    byte[] header = new byte[HEADER_SIZE - 2];
    int off = FragPType.CopyTo(header, 0);
    NumberSerializer.WriteInt((int)crc32, header, off);
    NumberSerializer.WriteInt(id, header, off + 4);
    MemBlock header_mb = MemBlock.Reference(header);
     
    ushort block = 0;
    int blocksize = ComputeBlockSize(len);
    while( rest.Length > 0) {
      int this_size = System.Math.Min(blocksize, rest.Length);
      MemBlock this_block = rest.Slice(0, this_size);
      rest = rest.Slice(this_size);
      //Check to see if this is the last block:
      if( rest.Length == 0 ) {
        block = (ushort)(block ^ (0x8000)); //Set the highest bit, this is last
      }
      byte[] block_b = new byte[2];
      NumberSerializer.WriteShort((short)block, block_b, 0);
      MemBlock block_mb = MemBlock.Reference(block_b);
      
      _wrapped_sender.Send(new CopyList(header_mb, block_mb, this_block));
      block += 1;
    }
  }
  public System.String ToUri() {
    var kv = new SCG.Dictionary<string,string>();
    kv["wrap"] = _wrapped_sender.ToUri();
    kv["max"] = MAX_PAYLOAD_SIZE.ToString();
    return SenderFactory.EncodeUri("frag", kv);
  }

}

/**
 * This handles fragmented packets
 */
public class FragmentingHandler : SimpleSource, IDataHandler {

  private readonly Cache _fragments;
  /**
   * TODO evaluate this choice, idea, make sure final packet is much less than 1500,
   * if we don't keep it, significantly under, TunnelEdges/Forwarding might have some
   * problems.  So, 1300 should give us a 200 byte headroom for other layers on top.
   */
  private static readonly int DEFAULT_SIZE = 1300; 
  private readonly SCG.Dictionary<Pair<uint, int>, Fragments> _frag_count;

  private class Fragments {
    public int Total;
    public ushort ReceivedCount;
    public Fragments() { Total = -1; ReceivedCount = 0; }
    public bool AddBlock(ushort blockid) {
      ReceivedCount += 1;
      if( blockid > FragmentingSender.MAX_BLOCK_CNT ) {
        Total = (blockid ^ FragmentingSender.LAST_BLOCK_FLAG) + 1;
      }
      return Total == ReceivedCount;
    }
    //Return true when we have none of the blocks anymore
    public bool RemoveBlock() {
      ReceivedCount -= 1;
      return ReceivedCount == 0;
    }
  }

  public FragmentingHandler(int max_frags_cached) : base() {
    _fragments = new Cache(max_frags_cached);
    _fragments.EvictionEvent += this.HandleEviction;
    _frag_count = new SCG.Dictionary<Pair<uint, int>, Fragments>();
  }

  protected MemBlock DecodeAndClear(uint crc32, int id, ushort total) {
    ICopyable[] blocks = new ICopyable[total];
    bool missing_block = false;
    ushort last_idx = (ushort)(total - 1);
    for(ushort i = 0; i < last_idx; i++) {
      var key = new Triple<uint, int, ushort>(crc32, id, i);
      var this_block = (MemBlock)_fragments.Remove(key);
      if( null != this_block ) {
        blocks[i] = this_block;
      }
      else {
        //We are missing one:
        missing_block = true;
        break;
      }
    }
    //Get the last one:
    ushort last_id = (ushort)(last_idx ^ FragmentingSender.LAST_BLOCK_FLAG);
    var lastkey = new Triple<uint, int, ushort>(crc32, id, last_id);
    var last_block = (MemBlock)_fragments.Remove(lastkey);
    blocks[last_idx] = last_block;

    missing_block = missing_block || (null == last_block);
    
    MemBlock result = null;
    if( false == missing_block ) {
      result = MemBlock.Concat(blocks);
      uint crc32_ck = result.Read<uint>(Crc32.ComputeChecksum);
      if( crc32_ck != crc32 ) {
        //Something went wrong:
        result = null;
      }
    }
    _frag_count.Remove(new Pair<uint,int>(crc32, id));
    return result;
  }

  public void HandleData(MemBlock b, ISender return_path, object state) {
    //Read the header:
    uint crc32 = (uint)NumberSerializer.ReadInt(b, 0);
    int id = NumberSerializer.ReadInt(b, 4);
    ushort block = (ushort)NumberSerializer.ReadShort(b, 8);
    MemBlock data = b.Slice(10);
    var cachekey = new Triple<uint, int, ushort>(crc32, id, block);
    MemBlock packet = null;
    lock(_sync) {
      if( false == _fragments.Contains(cachekey) ) {
        //This is a new block:
        _fragments.Add(cachekey, data);
        var fc_key = new Pair<uint, int>(crc32, id);
        Fragments this_fc;
        if( false == _frag_count.TryGetValue(fc_key, out this_fc) ) {
          this_fc = new Fragments();
          _frag_count.Add(fc_key, this_fc);
        }
        if( this_fc.AddBlock(block) ) {
          //We have all of them, decode and clean up:
          packet = DecodeAndClear(crc32, id, (ushort)this_fc.Total);
        }
      }
    }
    if( null != packet ) {
      Handle(packet, new FragmentingSender(DEFAULT_SIZE, return_path));
    }
  }
 
  /*
   * The lock is always being held when we Add, and thus when this
   * is called
   * This cleans out all the blocks with same id and crc32, since
   * loosing a block (probably) means we can't recover.
   */ 
  protected void HandleEviction(object cache, System.EventArgs evargs) {
    var arg = (Cache.EvictionArgs)evargs;
    var cachekey = (Triple<uint,int,ushort>)arg.Key;
    Fragments this_fc;
    var fc_key = new Pair<uint, int>(cachekey.First, cachekey.Second);
    if( _frag_count.TryGetValue(fc_key, out this_fc) ) {
      if( this_fc.RemoveBlock() ) {
        //We have no more fragments:
        _frag_count.Remove(fc_key);
      }
      /*
       * If we haven't seen the last packet, just assume it will be the next
       * one
       */
      ushort last_idx = (ushort)(this_fc.Total > 0 ? this_fc.Total - 1 :
                                                     cachekey.Third + 1);
      for(ushort i = 0; i < last_idx; i++) {
        var k = new Triple<uint, int, ushort>(cachekey.First, cachekey.Second, i);
        var o = _fragments.Remove(k);
        if( null != o && this_fc.RemoveBlock() ) {
          _frag_count.Remove(fc_key);
        }
      }
      //Try to remove the last:
      var lastk = new Triple<uint, int, ushort>(cachekey.First,
                                    cachekey.Second,
                                    (ushort)(last_idx ^ FragmentingSender.LAST_BLOCK_FLAG));
      var lasto = _fragments.Remove(lastk);
      if( null != lasto && this_fc.RemoveBlock() ) {
        _frag_count.Remove(fc_key);
      }
    }
  }
}

#if BRUNET_NUNIT
[TestFixture]
public class FragTest {

 public class FragPipe : SimpleSource, ISender {
  public static readonly FragPipe Instance = new FragPipe();
  static FragPipe() {
    SenderFactory.Register("pipe", delegate(object o, string uri) { return
    Instance; });
    
  }
  protected FragPipe() { }
  public void Send(ICopyable data) {
    MemBlock mb = data as MemBlock;
    if( null == mb ) {
      mb = MemBlock.Copy(data);
    }
    Handle(mb, this);
  }
  public System.String ToUri() { return "sender:pipe"; }
 }
 public class TestHandler : IDataHandler {
   public MemBlock LastData;
   public void HandleData(MemBlock mb, ISender from, object o) {
     LastData = mb;
   }
 }
 public class HeaderChop : SimpleSource, IDataHandler {
   public readonly MemBlock Header;
   public readonly SimpleSource WithoutHeader;
   public HeaderChop(MemBlock header) : base() {
     Header = header;
     WithoutHeader = new SimpleSource();
   }
   public void HandleData(MemBlock mb, ISender from, object state) {
     int len = Header.Length;
     if( mb.Length >= len ) {
       var temph = mb.Slice(0, len);
       if( temph.Equals( Header) ) {
         Handle(mb.Slice(len), from);
         return;
       }
     }
     WithoutHeader.Handle(mb, from);
   }
 }

 [Test]
 public void EqTest() {
   var fp = FragPipe.Instance; //Get some sender
   var fs1 = new FragmentingSender(100, fp);
   var fs2 = new FragmentingSender(100, fp);
   Assert.AreEqual(fs1, fs2);
   Assert.AreEqual(fs1.GetHashCode(), fs2.GetHashCode());
   //urlencode sender:pipe
   Assert.AreEqual(fs1.ToUri(), "sender:frag?max=100&wrap=sender%3apipe");
   Assert.AreEqual(fs1, SenderFactory.CreateInstance(null, fs1.ToUri()), "uri RT");
 }
 [Test]
 public void TestSend() {
   var fp = FragPipe.Instance;
   
   var head_c = new HeaderChop(new PType("frag").ToMemBlock());
   fp.Subscribe(head_c, null);

   var fh = new FragmentingHandler(1000);
   head_c.Subscribe(fh, null);

   var th = new TestHandler();
   fh.Subscribe(th, null);
   head_c.WithoutHeader.Subscribe(th, null);

   var fs = new FragmentingSender(100, fp);
   var r = new System.Random();
   for(int length = 1; length < 10000; length++) {
     var buf = new byte[length];
     r.NextBytes(buf);
     var dat = MemBlock.Reference(buf);
     fs.Send(dat); //This will do the assert.
     Assert.AreEqual(dat, th.LastData, "Data was received");
   }
 }

}
#endif
}
