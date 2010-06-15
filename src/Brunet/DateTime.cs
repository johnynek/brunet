/*
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida

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

based on ...
//
// System.DateTime.cs
//
// author:
//   Marcel Narings (marcel@narings.nl)
//   Martin Baulig (martin@gnome.org)
//   Atsushi Enomoto (atsushi@ximian.com)
//
//   (C) 2001 Marcel Narings
// Copyright (C) 2004-2006 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
*/

#if BRUNET_SIMULATOR
using System;

namespace Brunet {
	public class DateTime : IComparable, IComparable<DateTime> {
    protected long _ticks;
    public long Ticks { get { return _ticks; } }
		public static readonly DateTime MaxValue = new DateTime(long.MaxValue);
		public static readonly DateTime MinValue = new DateTime(0);
    public const long MAX_VALUE_TICKS = 3155378975999999999L;

    public static void Increment() {
      _cticks += TimeSpan.TicksPerMillisecond;
    }

    public static void SetTime(long now) {
      _cticks = now;
    }

    public static void Increment(long ms) {
      _cticks += ms;
    }

    protected static long _cticks = System.DateTime.UtcNow.Ticks;
    public static DateTime UtcNow {
      get {
        return new DateTime(_cticks);
      }
    }

    public DateTime(long ticks) {
      _ticks = ticks;
    }

		public DateTime Add(TimeSpan value) {
      return AddTicks(value.Ticks);
    }

		public DateTime AddDays(double value) {
			return AddMilliseconds (Math.Round (value * 86400000));
		}
		
		public DateTime AddTicks(long value) {
			if ((value + _ticks) > MAX_VALUE_TICKS || (value + _ticks) < 0) {
				throw new ArgumentOutOfRangeException();
			}
			return new DateTime(value + _ticks);
		}

		public DateTime AddHours(double value) {
			return AddMilliseconds(value * 3600000);
		}

		public DateTime AddMilliseconds(double value) {
			if ((value * TimeSpan.TicksPerMillisecond) > long.MaxValue ||
					(value * TimeSpan.TicksPerMillisecond) < long.MinValue) {
				throw new ArgumentOutOfRangeException();
			}
			long msticks = (long) (value * TimeSpan.TicksPerMillisecond);

			return AddTicks(msticks);
		}

		public DateTime AddMinutes(double value) {
			return AddMilliseconds(value * 60000);
		}
		
    // This is a lazy implementation
		public DateTime AddMonths(int months) {
      return AddDays(90);
		}

		public DateTime AddSeconds(double value) {
			return AddMilliseconds(value * 1000);
		}

		public DateTime AddYears(int value) {
			return AddMonths (value * 12);
		}

		public static int Compare(DateTime t1,	DateTime t2) {
			if (t1.Ticks < t2.Ticks) 
				return -1;
			else if (t1.Ticks > t2.Ticks) 
				return 1;
			else
				return 0;
		}

		public int CompareTo (object value) {
			if (value == null)
				return 1;

			if (!(value is System.DateTime))
				throw new ArgumentException("Value is not a System.DateTime");

			return Compare(this, (DateTime) value);
		}

		public int CompareTo (DateTime value) {
			return Compare (this, value);
		}

		public bool Equals (DateTime value) {
			return value.Ticks == Ticks;
		}

		public override bool Equals(object value) {
			if (!(value is System.DateTime))
				return false;

			return ((DateTime) value).Ticks == Ticks;
		}

		public static bool Equals(DateTime t1, DateTime t2) {
			return (t1.Ticks == t2.Ticks);
		}

		public override int GetHashCode () {
			return (int) _ticks;
		}

		public TimeSpan Subtract (DateTime value) {
			return new TimeSpan(Ticks - value.Ticks);
		}

		public DateTime Subtract(TimeSpan value) {
      return new DateTime(Ticks - value.Ticks);
		}

		public override string ToString () {
			return "Ticks: " + (Ticks / TimeSpan.TicksPerMillisecond);
		}

		public static DateTime operator +(DateTime d, TimeSpan t) {
      return new DateTime(d.Ticks + t.Ticks);
		}

		public static bool operator ==(DateTime d1, DateTime d2) {
			return (d1.Ticks == d2.Ticks);
		}

		public static bool operator >(DateTime t1,DateTime t2) {
			return (t1.Ticks > t2.Ticks);
		}

		public static bool operator >=(DateTime t1,DateTime t2) {
			return (t1.Ticks >= t2.Ticks);
		}

		public static bool operator !=(DateTime d1, DateTime d2) {
			return (d1.Ticks != d2.Ticks);
		}

		public static bool operator <(DateTime t1,	DateTime t2) {
			return (t1.Ticks < t2.Ticks );
		}

		public static bool operator <=(DateTime t1,DateTime t2) {
			return (t1.Ticks <= t2.Ticks);
		}

		public static TimeSpan operator -(DateTime d1,DateTime d2) {
			return new TimeSpan((d1.Ticks - d2.Ticks));
		}

		public static DateTime operator -(DateTime d,TimeSpan t) {
			return new DateTime (d.Ticks - t.Ticks);
		}
	}
}
#endif
