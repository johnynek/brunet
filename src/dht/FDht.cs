using System;
using System.Text;
using System.Collections;
using System.Security.Cryptography;

#if DHT_LOG
using log4net;
using log4net.Config;
#endif


using Brunet;
using Brunet.Dht;

namespace Brunet.Dht {	
  public class FDht:Dht {
#if DHT_LOG
    private static readonly log4net.ILog _log =
    log4net.LogManager.GetLogger(System.Reflection.MethodBase.
				 GetCurrentMethod().DeclaringType);
#endif
    //degree of replication is 2**degree
    private int _degree;
    public int Degree {
      get {
	return _degree;
      }
    }
    public FDht(Node node, EntryFactory.Media media, int n): base(node, media) 
    {
      _degree = (int) System.Math.Pow(2, n);
    }
    
    public static byte[][] MapToRing(byte[] key, int num_targets) {
      HashAlgorithm hashAlgo = HashAlgorithm.Create();
      byte[] hash = hashAlgo.ComputeHash(key);
      hash[Address.MemSize -1] &= 0xFE;

      //find targets which are as far apart on the ring as possible
      byte[][] target = new byte[num_targets][];
      target[0] = hash;
      Address.SetClass(target[0], AHAddress._class);      

      //add these increments to the base address
      BigInteger inc_addr = Address.Full/num_targets; 

      BigInteger curr_addr = new BigInteger(target[0]);
      for (int k = 1; k < target.Length; k++) {
	//add an increment
	curr_addr = curr_addr + inc_addr;
	target[k] = Address.ConvertToAddressBuffer(curr_addr);
	Address.SetClass(target[k], AHAddress._class);
      }
      return target;
    }

    public BlockingQueue[] PutF(byte[] key, int ttl, string hashed_password, byte[] data) {
#if DHT_DEBUG
      Console.Error.WriteLine("[DhtClient] Invoking a Dht::Put()");
#endif
      if (!_activated) {
#if DHT_DEBUG
	Console.Error.WriteLine("[DhtClient] Not yet activated. Throwing exception!");
#endif	
	throw new DhtException("DhtClient: Not yet activated.");
      }
      byte[][] b = MapToRing(key, _degree);

      BlockingQueue[] q = new BlockingQueue[_degree];
      for (int k = 0; k < _degree; k++) {
	Address target = new AHAddress(b[k]);
#if DHT_LOG
	_log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::InvokePut::::" +
		   + Base32.Encode(b[k]) + "::::" + target);
#endif
	q[k] = _rpc.Invoke(target, "dht.Put", b[k], ttl, hashed_password, data);
      }
      return q;
    }

    public BlockingQueue[] CreateF(byte[] key, int ttl, string hashed_password, byte[] data) {
#if DHT_DEBUG
      Console.Error.WriteLine("[DhtClient] Invoking a Dht::Create()");
#endif

      if (!_activated) {
#if DHT_DEBUG
	Console.Error.WriteLine("[DhtClient] Not yet activated. Throwing exception!");
#endif	
	throw new DhtException("DhtClient: Not yet activated.");
      }
      
      byte[][] b = MapToRing(key, _degree);

      BlockingQueue[] q = new BlockingQueue[_degree];
      for (int k = 0; k < _degree; k++) {
	Address target = new AHAddress(b[k]);
#if DHT_LOG
	_log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::InvokeCreate::::" +
		   + Base32.Encode(b[k]) + "::::" + target);
#endif
	q[k] = _rpc.Invoke(target, "dht.Create", b[k], ttl, hashed_password, data);
      }
      return q;
    }

    public BlockingQueue[] RecreateF(byte[] key, int ttl, string hashed_password, byte[] data) {
#if DHT_DEBUG
      Console.Error.WriteLine("[DhtClient] Invoking a Dht::Recreate()");
#endif

      if (!_activated) {
#if DHT_DEBUG
	Console.Error.WriteLine("[DhtClient] Not yet activated. Throwing exception!");
#endif	
	throw new DhtException("DhtClient: Not yet activated.");
      }
      
      byte[][] b = MapToRing(key, _degree);

      BlockingQueue[] q = new BlockingQueue[_degree];
      for (int k = 0; k < _degree; k++) {
	Address target = new AHAddress(b[k]);
#if DHT_LOG
	_log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::InvokeRecreate::::" +
		   + Base32.Encode(b[k]) + "::::" + target);
#endif
	q[k] = _rpc.Invoke(target, "dht.Recreate", b[k], ttl, hashed_password, data);
      }
      return q;
    }
    
    public BlockingQueue[] GetF(byte[] key, int maxbytes, byte[] token) {
#if DHT_DEBUG
      Console.Error.WriteLine("[DhtClient] Invoking a Dht::Get()");
#endif
      if (!_activated) {
#if DHT_DEBUG
	Console.Error.WriteLine("[DhtClient] Not yet activated. Throwing exception!");
#endif	
	throw new DhtException("DhtClient: Not yet activated.");
      }

      byte[][] b = MapToRing(key, _degree);
      BlockingQueue[] q = new BlockingQueue[_degree];
      for (int k = 0; k < _degree; k++) {
	Address target = new AHAddress(b[k]);
#if DHT_LOG
	_log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::InvokeGet::::" +
		   + Base32.Encode(b[k]) + "::::" + target);
#endif      
	q[k] = _rpc.Invoke(target, "dht.Get", b[k], maxbytes, token);
      }
      return q;
    }
    public BlockingQueue[] DeleteF(byte[] key, string password)
    {  
#if DHT_DEBUG
      Console.Error.WriteLine("[DhtClient] Invoking a Dht::Delete()");
#endif

      if (!_activated) {
#if DHT_DEBUG
	Console.Error.WriteLine("[DhtClient] Not yet activated. Throwing exception!");
#endif	
	throw new DhtException("DhtClient: Not yet activated.");
      }

      byte[][] b = MapToRing(key, _degree);
      BlockingQueue[] q = new BlockingQueue[_degree];
      for (int k = 0; k < _degree; k++) {
	Address target = new AHAddress(b[k]);
#if DHT_LOG
	_log.Debug(_node.Address + "::::" + DateTime.UtcNow.Ticks + "::::InvokeDelete::::" +
		   + Base32.Encode(b[k]) + "::::" + target);
#endif
	q[k] = _rpc.Invoke(target, "dht.Delete", b[k], password);
      }
      return q;
    }
  }
}
