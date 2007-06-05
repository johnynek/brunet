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
      bool new_get = false;
      // The below code sorts through the tokens to ensure we send the token to the correct node
      if(token == null) {
        token = new byte[_degree][];
        new_get = true;
      }
      else {
        byte [][]actual_token = new byte[_degree][];
        for (int i = 0; i < token.Length; i++) {
          int[] bounds = (int[])AdrConverter.Deserialize(new System.IO.MemoryStream(token[i]));
          actual_token[bounds[0]] = token[i];
        }
        token = actual_token;
      }

      for (int k = 0; k < _degree; k++) {
        Address target = new AHAddress(MemBlock.Reference(b[k]));
        AHSender s = new AHSender(_rpc.Node, target);
        q[k] = new BlockingQueue();
        // The below creates a token for first time creates
        if(token[k] == null && new_get) {
          int[] data = new int[]{k, -1, -1};
          System.IO.MemoryStream ms = new System.IO.MemoryStream();
          AdrConverter.Serialize(data, ms);
          token[k] = ms.ToArray();
        }
        if(token[k] != null) {
          _rpc.Invoke(s,q[k], "dht.Get", b[k], maxbytes, token[k]);
        }
      }
      return q;
    }
  }
}
