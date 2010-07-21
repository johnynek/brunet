/*
 * From: http://sanity-free.org/12/crc32_implementation_in_csharp.html
 * apparently in public domain.  Made some comments and refactored the code
 * 
 * Basic idea: represent a stream of bits (MSB first) as a polynomial:
 * 11010111 = 1 + x + x^3 + x^5 + x^6 + x^7.
 * then, we take this polynomial modulo an irreducible element of GF(2^32).
 * In this case, we use the polynomial used by the Ethernet standard:
 * http://en.wikipedia.org/wiki/Cyclic_redundancy_check#Commonly_used_and_standardized_CRCs
 */
#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Util {
    public class Crc32 {
      /**
       * This table holds the modulo of all the
       * terms like: a_32 x^32 + a_33 x^33 + .. + a_39 x^39
       * there are 256 such terms.  It is relatively expensive
       * to do this operation, so the table lookup is the fastest
       * way to go: construct the table once, and use it from
       * there on out.
       */
        protected readonly uint[] TABLE;
        //Most common CRC, in unix cksum, ethernet:
        public const uint ETHERNET_CRC = 0xedb88320;
        //Castagnoli CRC Used in iSCSI, SCTP, has better error properties
        public const uint CRC_C = 0x82F63B78;
        protected static Crc32 _cast;
        public static Crc32 Castagnoli {
          get {
            if( null == _cast ) {
              _cast = new Crc32(CRC_C);
            }
            return _cast;
          }
        }
        protected static Crc32 _ethernet;
        public static Crc32 Ethernet {
          get {
            if( null == _ethernet ) {
              _ethernet = new Crc32(ETHERNET_CRC);
            }
            return _ethernet;
          }
        }
        
// /////////////
// Constructors
// /////////////

        public Crc32(uint poly) {
          TABLE = MakeTable(poly);
        }

// /////////////
// Methods
// /////////////

        /*
         * 
         * you can chain multiple calls of this together if you pass the
         * previous result to the initcrc.  It should start at 0 for the first
         * call
         */
        public uint Compute(uint initcrc, byte[] bytes, int offset, int length) {
            uint crc = ~initcrc;
            for(int i = offset; i < length; ++i) {
                /*
                 * bytes[i] is the next 8 terms in the polynomial
                 * when we take the high (in this ordering) 8 terms
                 * from the existing CRC, we get terms of powers:
                 * x^32, x^33, ... x^39, just what the table knows
                 * the values of:
                 */
                uint pows32_to_39 = (crc & 0xff) ^ bytes[i];
                crc = (uint)((crc >> 8) ^ TABLE[pows32_to_39]);
            }
            return ~crc;
        }
        //Use Ethernet polynomial by default:
        public static uint ComputeChecksum(uint initcrc, byte[] bytes, int offset, int length) {
          return Ethernet.Compute(initcrc, bytes, offset, length);
        }
        public static uint ComputeChecksum(byte[] bytes, int offset, int length) {
          return ComputeChecksum(0, bytes, offset, length);
        }

        public static byte[] ComputeChecksumBytes(byte[] bytes, int offset, int length) {
            int crc = (int)ComputeChecksum(bytes,offset,length);
            var res = new byte[4];
            NumberSerializer.WriteInt(crc, res, 0);
            return res;
        }

        /*
         * compute the modulo for all terms:
         * a_32 x^32 + a_33 x^33 + ... + a_39 x^39
         * by using the rule: x^32 = poly
         */
        public static uint[] MakeTable(uint poly) {
            uint[] tab = new uint[256];
            uint temp = 0;
            for(uint i = 0; i < 256; ++i) {
                temp = i;
                /*
                 * \sum_i={32}^{39} a_i x^i = x^{32} \sum_{i=0}^7 a_{i+32} x^i
                 *                          = p \sum_{i=0}^7 a_{i+32} x^i
                 *                          = \sum_{i=0}^7 a_{i+32} (p >> i)
                 * The below is a representation of the above mathematics:
                 * remember ^ is plus in GF(2^n), and (a >> 1) == (a * x)
                 */
                for(int j = 8; j > 0; --j) {
                    if((temp & 1) == 1) {
                        temp = (uint)((temp >> 1) ^ poly);
                    }else {
                        temp >>= 1;
                    }
                }
                tab[i] = temp;
            }
            return tab;
        }

    }
#if BRUNET_NUNIT
[TestFixture]
public class Crc32Test {

  [Test]
  public void TestCRCValues() {
    /*
     * Here are just a few values taken from an online calculator
     */
    Assert.AreEqual(0x8BB98613,
      Crc32.Ethernet.Compute(0,new byte[]{0x0,0x1,0x2,0x3},0,4));
    Assert.AreEqual(0x515AD3CC,
      Crc32.Ethernet.Compute(0,new byte[]{0x0,0x1,0x2,0x3,0x4},0,5));
    Assert.AreEqual(0x30EBCF4A,
      Crc32.Ethernet.Compute(0,new byte[]{0x0,0x1,0x2,0x3,0x4,0x5},0,6));
  }

}

#endif
}
