/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2008 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

using System;
using System.Security.Cryptography;
#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Util {

/**
 * This class allows you to use a RandomNumberGenerator as a Random object.
 * This is useful to generate secure random integers which
 * RandomNumberGenerator cannot do, it can only return bytes.
 */
#if BRUNET_NUNIT
[TestFixture]
#endif
public class SecureRandom : System.Random {

  //Use the GetBytes method to get random bytes
  protected readonly RandomNumberGenerator _rng;
  protected readonly byte[] _sample_buffer;
  protected Sample _state;
  
  /** 
   * Create a System.Random object from this RandomNumberGenerator
   */
  public SecureRandom(RandomNumberGenerator rng) {
    _rng = rng;
    _sample_buffer = new byte[4];
    _state = new Sample();
    _state.Val = 0;
    _state.Max = 1;
  }

  /**
   * Make a SecureRandom with the default RandomNumberGenerator
   */
  public SecureRandom() : this(RNGCryptoServiceProvider.Create()) {
  
  }

  /*
   * Inner classes to handle samples
   */
  protected struct Sample {
    public ulong Val;
    public ulong Max;

    public static Sample operator * (Sample s1, Sample s2) {
      /*
       * Two samples can be multiplied together to get a new
       * one.  The new max value is the product, the new sample
       * is s1.Val * s2.Max + s2.Val
       */
      Sample new_s = new Sample();
      new_s.Val = s1.Val * s2.Max + s2.Val;
      new_s.Max = s1.Max * s2.Max;
      return new_s;
    }
    /**
     * use with the below, but you can't reuse the sample
     * you are splitting with / and %
     *
     * @returns a new random sample over max
     */
    public static Sample operator % (Sample s, uint max) {
      if (s.Max % max != 0) {
        throw new ArgumentException();
      }
      Sample new_s = new Sample();
      new_s.Val = s.Val % max;
      new_s.Max = max;
      return new_s;
    }
    /**
     * use with the above, but you can't reuse the sample
     * you are splitting with / and %
     *
     * @returns a new random sample over s.Max / max
     */
    public static Sample operator / (Sample s, uint max) {
      if (s.Max % max != 0) {
        throw new ArgumentException();
      }
      Sample new_s = new Sample();
      new_s.Max = s.Max / max;
      new_s.Val = s.Val / max;
      return new_s;
    }
    /**
     * convert it so you can split the sample with max.
     * it may fail.  If it fails, you have to get a new
     * sample and multiply and try again.
     * @todo the output of this is a biased random coin, we should reuse the
     * entropy.
     * @return true if the truncation worked.
     */ 
    public bool Truncate(uint max) {
      if( Max < max ) { return false; }
      if( Max == max ) { return true; }
      ulong partner = Max / max;
      ulong new_max = partner * max;
      if (Val < new_max) {
        Max = new_max;
        return true; 
      }
      else {
        Val -= new_max;
        Max -= new_max;
        return false;
      }
    }
  }
  /*
   * Here are the methods from System.Random
   */

  /**
   * Return a random number over the range [0, Int32.MaxValue)
   */
  public override int Next() {
    return Next(0, Int32.MaxValue);
  }

  /**
   * Return a random number over the range [0, high)
   */
  public override int Next(int high) {
    return Next(0, high);
  }

  /**
   * Return a random number over the range [low, high)
   * if low == high or low == high + 1, returns low.
   */
  public override int Next(int low, int high) {
    if( low > high ) {
      throw new ArgumentOutOfRangeException("low",low,"low is greater than high");
    }
    if( (low + 1) >= high ) {
      //This is the case were low == high or low+1 == high
      return low;
    }
    //Else, high is strictly greater
    uint max = (uint)(high - low);
    Sample s = Pop(max);
    return (int)((uint)s.Val + low);
  }

  /**
   * fill the array with random bytes
   */
  public override void NextBytes(byte[] tofill) {
    _rng.GetBytes(tofill);
  }

  /**
   * return a random double in the range [0.0, 1.0)
   */
  public override double NextDouble() {
    //Not done yet
    throw new System.NotImplementedException();
  }

  ///This pops out just enough randomness
  protected Sample Pop(uint max) {
    ///@todo truncation loses as much as 1-bit of entropy.  Recycle it!
    while( !_state.Truncate(max) ) {
      _state = _state * NextSample();
    }
    //Now we can certainly split _state:
    Sample new_s = _state % max;
    _state = _state / max;
    return new_s;
  }

  ///This generates a new Sample uniform over the [0, 2^{32})
  protected Sample NextSample() {
    uint val = 0;
    _rng.GetBytes(_sample_buffer);
    for(int i= 0; i < 4; i++) {
      val = (val << 8) | _sample_buffer[i];
    }
    Sample s = new Sample();
    s.Val = val;
    s.Max = 0x100000000L; //This is 2^{32} 
    return s;
  }

#if BRUNET_NUNIT
  //Test methods
 
  /*
   * Ideally we would have hits * bins ~= count
   * This should pass except with probability less than eps
   */
  public static bool TestElement(int hits, int bins, int count, double eps) {
    if( bins == 1 ) {
      //This is a special case:
      return (hits == count);
    }
    //Use Chernoff bound to test:
    double p = 1.0/(double)bins;
    double variance = p*(1.0 - p);
    
    double dhits = (double)hits;
    double mean = count * p;
    double sigma = Math.Sqrt( count * variance );
    double deviation = Math.Abs(dhits - mean);
    double k = 2*Math.Sqrt(Math.Log(2.0) - Math.Log(eps));
    return (deviation < k*sigma);
  }
  
  public void Test(int low, int high, int count, double eps) {
    int m = high - low;
    int[] counts = new int[m];
    for(int i = 0; i < count; i++) {
      int sample = Next(low, high) - low;
      counts[sample] = counts[sample] + 1;
    }
    for(int i = 0; i < m; i++) {
      bool result = TestElement(counts[i], m, count, eps);
      Assert.IsTrue(result,
              String.Format("too much deviation: low: {2} high: {3} trials: {4} "
                           + "count[{0}] = {1}", i, counts[i], low, high, count));
    }
  }
  [Test]
  public void Test100() {
  
    const int MAX = 100;
    double dmax = (double)MAX;
    double eps;
    for(int i = 1; i < MAX; i++ ) {
      for( int j = -i; j < i; j++) {
        double possibilities = (double)(i - j);
        //We should have less than 1/2 a fail per run
        eps = 0.5 / (dmax * dmax * possibilities);
        Test(j, i, 10*MAX, eps);
      }
    }
  }
  
  public void TestCoincidence(int count) {
    //Check for coincidences:
    const int MAX = 1000;
    int hits = 0;
    int last = Next(count);
    for(int i = 0; i < MAX; i++) {
      int this_sample = Next(count);
      if (this_sample == last) { 
        hits++;
      }
      last = this_sample;
    }
    Assert.IsTrue(TestElement(hits, count, MAX, 0.01/(double)MAX),
       String.Format("Coincidence: 1 in {0} but: {1} in {2}",count, hits, MAX));
  }
  [Test]
  public void TestCoincidences() {
    for(int i = 1; i < 10; i++) {
      TestCoincidence(i);
    }
  }
  [Test]
  public void TestNoRandom() {
    const int MAX = 1000;
    for(int i = 0; i < MAX; i++) {
      int low = Next();
      Assert.AreEqual(Next(low, low),low,
                      String.Format("Next({0},{0}) != {0}", low));
      Assert.AreEqual(Next(low, low+1),low,
                      String.Format("Next({0},{1}) != {0}", low, low+1));
    }
   
  }
#endif
}

}
