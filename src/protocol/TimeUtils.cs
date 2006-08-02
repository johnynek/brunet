/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2006 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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

#if BRUNET_NUNIT
using System.Threading;
using NUnit.Framework;
#endif

namespace Brunet {

 /**
  * We often need timing tools that the .Net library
  * does not provide, or does not provide sufficiently
  * high performance APIs for.
  */
#if BRUNET_NUNIT
 [TestFixture] 
#endif
 public class TimeUtils {


   static protected long _t_zero_l;
   static protected int _t_zero_i;
   static object _sync;
   static TimeUtils() {
     _t_zero_i = System.Environment.TickCount;
     _t_zero_l = System.DateTime.Now.Ticks;
     _sync = new object();
     //Those two numbers represent the same instants in time
   }
   /**
    * This returns a long representing the current time
    * suitable for use with the DateTime(long) constructor
    * However, this time is not very accurate.  The accuracy
    * is at least 500 milliseconds, but perhaps not more accurate.
    * This is often sufficient for imprecise timers.
    * 
    * On the other hand, this should be *MUCH* faster than
    * DateTime.Now (by around 3 orders of magnitude according
    * to mono --profile).  This is important if you put it in
    * a latency sensitive situation.
    *
    * In .Net, this number should represent the number of
    * 100-nanosecond intervals since 0:00 1/1/0001
    *
    * Note that 1 millisecond = 10^{-3} s.
    * 1 nanosecond= 10^{-9} s
    * 100 nanosecond = 10^{-7} s = 10^{-4} milliseconds
    */
   public static long NoisyNowTicks {
     get {
       int now = System.Environment.TickCount;
       //1 millisecond is 10000 of 100 nanoseconds.
       long delta_ms = WrappedDifference(_t_zero_i, now);
       long return_val = MsToNsTicks(delta_ms) + _t_zero_l;
       if( delta_ms > (Int32.MaxValue/2) ) {
         //two wrap arounds will confuse us, better update:
         //note that a wrap around will happen about once a month
         lock( _sync ) {
           _t_zero_i = System.Environment.TickCount;
           _t_zero_l = System.DateTime.Now.Ticks;
         }
       }
       return return_val;
     }
   }

   /**
    * Sometimes time is measured in milliseconds, sometimes
    * in 100 ns ticks.  This goes from ms -> 100 ns ticks
    */
   public static long MsToNsTicks(long millisec) {
     return millisec * 10000;
   }

   /**
    * b is always greater than a, if b is less than
    * a, it is because it has wrapped around.  What
    * @return difference between them
    */
   static protected long WrappedDifference(int a, int b) {
     if( b < a ) {
       long b_l = b + Int32.MaxValue;
       return (b_l - a);
     }
     else {
       return (b - a);
     }
   }
#if BRUNET_NUNIT
   //For the nunit tests, they need a constructor
   public TimeUtils() {

   }
   [Test]
   public void Test() {
     DateTime t0_dt = DateTime.Now;
     DateTime t0_n = new DateTime(TimeUtils.NoisyNowTicks);
     Thread.Sleep(2000); //Sleep 2 seconds;
     DateTime t1_dt = DateTime.Now;
     //System.Console.WriteLine("DateTime.Now: {0}", t1_dt);
     DateTime t1_n = new DateTime(TimeUtils.NoisyNowTicks);
     //System.Console.WriteLine("NoisyNow: {0}", t1_n);
     //Now see if the difference in is close:
     TimeSpan close = new TimeSpan(0,0,0,0,500); //They should be within 500 milliseconds;
     TimeSpan delta_dt = t1_dt - t0_dt;
     TimeSpan delta_n = t1_n - t0_n;
     TimeSpan diff;
     if( delta_n > delta_dt ) {
       diff = delta_n - delta_dt;
     }
     else {
       diff = delta_dt - delta_n;
     }
     //System.Console.WriteLine("diff is: {0}", diff);
     Assert.IsTrue(  diff < close, "NoisyNowTicks is close");
   }
#endif
 }

}
