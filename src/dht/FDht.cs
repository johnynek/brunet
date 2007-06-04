using System;
using System.Text;
using System.Collections;
using System.Security.Cryptography;

using Brunet;
using Brunet.Dht;

namespace Brunet.Dht {
  public class FDht:Dht {
    //degree of replication is 2**degree
    private int _degree;
    public int Degree {
      get {
        return _degree;
      }
    }

    public FDht(Node node, EntryFactory.Media media, int n): base(node, media) {
      _degree = (int) System.Math.Pow(2, n);
    }

    public static byte[][] MapToRing(byte[] key, int num_targets) {
      HashAlgorithm hashAlgo = HashAlgorithm.Create();
      byte[] hash = hashAlgo.ComputeHash(key);

      //find targets which are as far apart on the ring as possible
      byte[][] target = new byte[num_targets][];
      target[0] = hash;
      Address.SetClass(target[0], AHAddress._class);

      //add these increments to the base address
      BigInteger inc_addr = Address.Full/num_targets; 

      BigInteger curr_addr = new BigInteger(target[0]);
      for (int k = 1; k < target.Length; k++) {
        curr_addr = curr_addr + inc_addr;
        target[k] = Address.ConvertToAddressBuffer(curr_addr);
        Address.SetClass(target[k], AHAddress._class);
      }
      return target;
    }

    public BlockingQueue[] PutF(byte[] key, int ttl, string hashed_password, byte[] data) {
      if (!_dhtactivated) {
        throw new DhtException("DhtClient: Not yet activated.");
      }
      byte[][] b = MapToRing(key, _degree);

      BlockingQueue[] q = new BlockingQueue[_degree];
      for (int k = 0; k < _degree; k++) {
        Address target = new AHAddress(MemBlock.Reference(b[k]));
        AHSender s = new AHSender(_rpc.Node, target);
        q[k] = new BlockingQueue();
        _rpc.Invoke(s, q[k], "dht.Put", b[k], ttl, hashed_password, data);
      }
      return q;
    }

    public BlockingQueue[] CreateF(byte[] key, int ttl, string hashed_password, byte[] data) {
      if (!_dhtactivated) {
        throw new DhtException("DhtClient: Not yet activated.");
      }

      byte[][] b = MapToRing(key, _degree);

      BlockingQueue[] q = new BlockingQueue[_degree];
      for (int k = 0; k < _degree; k++) {
        Address target = new AHAddress(MemBlock.Reference(b[k]));
        AHSender s = new AHSender(_rpc.Node, target);
        q[k] = new BlockingQueue();
        _rpc.Invoke(s, q[k], "dht.Create", b[k], ttl, hashed_password, data);
      }
      return q;
    }

    public BlockingQueue[] GetF(byte[] key, int maxbytes, byte[][] token) {
      if (!_dhtactivated) {
        throw new DhtException("DhtClient: Not yet activated.");
      }

      byte[][] b = MapToRing(key, _degree);
      BlockingQueue[] q = new BlockingQueue[_degree];
      for (int k = 0; k < _degree; k++) {
        Address target = new AHAddress(MemBlock.Reference(b[k]));
        AHSender s = new AHSender(_rpc.Node, target);
        q[k] = new BlockingQueue();
        if(token != null) {
          _rpc.Invoke(s,q[k], "dht.Get", b[k], maxbytes, token[k]);
        }
        else {
          _rpc.Invoke(s,q[k], "dht.Get", b[k], maxbytes, null);
        }
      }
      return q;
    }
  }
}
