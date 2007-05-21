/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2006 P. Oscar Boykin <boykin@pobox.com>,  University of Florida

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
using System.IO;
using System.Collections;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet {
        
public class AdrException : Exception {
  
  /**
   * Use this one to make a new exception in a method designed to work
   * with these Adr Serialization
   */
  public AdrException(int code, string message) : base(message) {
    _data = new Hashtable();
    _data["code"] = code;
    _data["message"] = message;
  }
  /**
   * In serialization we map a given exception to one of ours
   */
  public AdrException(Exception x) {
    _data = new Hashtable();
    _data["message"] = x.Message;
    _data["stacktrace"] = x.StackTrace;
  }
  /**
   * In serialization we map a given exception to one of ours
   */
  public AdrException(int code, Exception x) {
    _data = new Hashtable();
    _data["code"] = code;
    _data["message"] = x.Message;
    _data["stacktrace"] = x.StackTrace;
  }
  public AdrException(Hashtable ht) {
    _data = ht;
  }

  protected Hashtable _data;
  
  public int Code {
    get {
      object c = _data["code"];
      if ( c == null ) {
        return 0;
      }
      else {
        return (int)c;
      }
    }
  }

  public override int GetHashCode() {
    return Code.GetHashCode() ^ Message.GetHashCode();
  }

  public override string Message {
    get {
      object m = _data["message"];
      if( m == null ) {
        return String.Empty;
      }
      else {
        return (string)m;
      }
    }
  }
  public override string StackTrace {
    get {
      object m = _data["stacktrace"];
      if( m == null ) {
        return base.StackTrace;
      }
      else {
        return (string)m;
      }
    }
  }

  public override bool Equals(object o) {
    if( o is AdrException ) {
      AdrException x = (AdrException)o;
      return (x.Code == this.Code) && (x.Message == this.Message)
             && (x.StackTrace == this.StackTrace);
    }
    else if (o is Exception ) {
      AdrException x = new AdrException((Exception)o);
      return this.Equals(x);
    }
    else {
      return false;
    }
  }
  public Hashtable ToHashtable() {
    Hashtable xht = (Hashtable)_data.Clone();
    object st = xht["stacktrace"];
    //The above is set when thrown:
    if( st == null && (this.StackTrace != String.Empty) ) {
      xht["stacktrace"] = this.StackTrace; 
    }
    return xht;
  }
  
}

/**
 * reads objects from binary arrays or streams
 * ADR stands for Another Data Representation.
 * This is similar to XDR or ASN.  Why not use those?  Couldn't find
 * a library for them and there is no type information in XDR, which
 * we wanted to include.
 *
 * see the adr_serialization.txt documentation file.
 */
public class AdrConverter {

  public static object Deserialize(Stream s) {
    bool finished = false;
    //The '_' character is a meaningless placeholder
    return Deserialize(s, '_', out finished);
  }

  public static object Deserialize(MemBlock mb) {
    return Deserialize( mb.ToMemoryStream() );
  }
  /*
   * This is how the above is implemented to support recursion
   */
  private static object Deserialize(Stream s, char terminator, out bool finished ) {
  
    int type = s.ReadByte();
    object result = null;
    finished = false; //Set the default value
    if( type < 0 ) {
      //This is an error
      throw new Exception("End of stream");
    }
    else {
      char typecode = (char)type;
      switch( typecode ) {
        case 'T':
          result = true;
          break;
        case 'F':
          result = false;
          break;
	case '0':
	  //Null:
	  result = null;
	  break;
	case 'b':
	  //Signed byte
	  int valy = s.ReadByte();
	  if( valy < 0 ) { throw new Exception("End of stream"); }
	  result = (sbyte)valy;
	  break;
	case 'B':
	  //Unsigned byte
	  int valY = s.ReadByte();
	  if( valY < 0 ) { throw new Exception("End of stream"); }
	  result = (byte)valY;
	  break;
        case 's':
	  //signed short:
	  result = NumberSerializer.ReadShort(s);
	  break;
        case 'S':
	  //unsigned short:
	  result = unchecked( (ushort)NumberSerializer.ReadShort(s) );
	  break;
        case 'i':
	  //signed int:
	  result = NumberSerializer.ReadInt(s);
	  break;
        case 'I':
	  //unsigned int:
	  result = unchecked( (uint)NumberSerializer.ReadInt(s) );
	  break;
        case 'l':
          //signed long:
          result = NumberSerializer.ReadLong(s);
          break;
        case 'L':
          //signed long:
          result = unchecked((ulong)NumberSerializer.ReadLong(s));
          break;
        case 'f':
	  //floating-point number
	  result = NumberSerializer.ReadFloat(s);
	  break;
	case '_':
	  //UTF-8 String:
          int bytelength = 0;
	  result = NumberSerializer.ReadString(s, out bytelength);
          break;
	case 'X':
	  //Start of an exception:
	  Hashtable xht = new Hashtable();
	  bool xfinished = false;
	  do {
	    //Magical recursion strikes again
            object key = Deserialize(s, 'x', out xfinished);
	    if( !xfinished ) {
              object valu = Deserialize(s);
	      xht.Add(key, valu);
	    }
	  } while (false == xfinished);
          result = new AdrException(xht);
	  break;
	case 'x':
	  //End of an exception:
	  if (terminator == 'x') {
            //We were reading a list and now we are done:
	    finished = true;
	  }
	  else {
            throw new Exception("Unexpected terminator: } != " + terminator);
	  }
	  result = null;
	  break;
	case 'a':
	  //Array:
	  //Read the length:
	  object olength = Deserialize(s);
          //Due to boxing here, we have to be careful about unboxing,
          //this will get easier with generics:
          long length = UnboxToLong(olength); 
	  int typec = s.ReadByte();
	  if ( typec < 0 ) { throw new Exception("Could not read array type"); }
	  char atype = (char)typec;
	  switch (atype) {
              case 'B':
	        //unsigned byte:
                byte[] aBresult = new byte[length];
                int read_b = s.Read(aBresult, 0, (int)length);
                if( read_b != length ) {
                  throw new Exception("Could not read byte for array"); 
                }
                result = aBresult;
                break;
              case 'i':
	        //signed int:
                int[] airesult = new int[length];
                for(int i = 0; i < airesult.Length; i++) {
                  airesult[i] = NumberSerializer.ReadInt(s);
                }
                result = airesult;
                break;
                ///@todo add more array types
              default:
                throw new Exception("Unsupported array type code: " + atype);
	  }
          break;
	case '(':
	  //Start of a list:
 	  IList listresult = new ArrayList();
	  bool lfinished = false;
          do {
	    //The magic of recursion.
            object tmp = AdrConverter.Deserialize(s, ')', out lfinished);
	    if( !lfinished ) {
	      //If we are finished, tmp holds a meaningless null
	      listresult.Add(tmp);
	    }
	  } while( false == lfinished );
          result = listresult;
          break;
	case ')':
          //End of the list:
	  if (terminator == ')') {
            //We were reading a list and now we are done:
	    finished = true;
	  }
	  else {
            throw new Exception("Unexpected terminator: ) != " + terminator);
	  }
	  result = null;
	  break;
	case '{':
	  //Start of a map:
	  IDictionary dresult = new Hashtable();
	  bool mfinished = false;
	  do {
	    //Magical recursion strikes again
            object key = Deserialize(s, '}', out mfinished);
	    if( !mfinished ) {
              object valu = Deserialize(s);
	      dresult.Add(key, valu);
	    }
	  } while (false == mfinished);
          result = dresult;
	  break;
	case '}':
	  //End of a map:
	  if (terminator == '}') {
            //We were reading a list and now we are done:
	    finished = true;
	  }
	  else {
            throw new Exception("Unexpected terminator: } != " + terminator);
	  }
	  result = null;
	  break;
        default:
          throw new Exception("Unexcepted typecode: " + typecode);
      }
      return result;
    }

  }
  
  /**
   * @return the number of bytes written into the stream
   */
  public static int Serialize(object o, Stream s) {
    if( o == null ) {
      //Not much work to do:
      s.WriteByte((byte)'0');
      return 1; //1 byte for null
    }
    
    //Else, o is some kind of object:
    
    System.Type t = o.GetType();
    if( t.Equals( typeof(bool) ) ) {
      //boolean value:
      bool b = (bool)o;
      if( b ) { s.WriteByte((byte)'T'); }
      else { s.WriteByte((byte)'F'); }
      return 1;
    }
    else if ( t.Equals(typeof(string)) ) {
      s.WriteByte((byte)'_');
      string val = (string)o;
      int bytes = NumberSerializer.WriteString(val, s);
      return 1 + bytes; //the typecode + the serialized string
    }
    else if ( t.Equals(typeof(byte)) ) {
      //Unsigned byte
      s.WriteByte((byte)'B');
      s.WriteByte((byte)o);
      return 2;
    }
    else if ( t.Equals(typeof(sbyte)) ) {
      s.WriteByte((byte)'b');
      long v = UnboxToLong(o);
      s.WriteByte((byte)v);
      return 2;
    }
    else if ( t.Equals(typeof(short)) ) {
      s.WriteByte((byte)'s');
      NumberSerializer.WriteShort((short)o,s);
      return 3; //1 typecode + 2 bytes for short
    }
    else if ( t.Equals(typeof(ushort)) ) {
      s.WriteByte((byte)'S');
      NumberSerializer.WriteUShort((ushort)o,s);
      return 3; //1 typecode + 2 bytes for short
    }
    else if ( t.Equals(typeof(int)) ) {
      s.WriteByte((byte)'i');
      NumberSerializer.WriteInt((int)o,s);
      return 5; //1 typecode + 4 bytes for int 
    }
    else if ( t.Equals(typeof(uint)) ) {
      s.WriteByte((byte)'I');
      NumberSerializer.WriteUInt((uint)o,s);
      return 5; //1 typecode + 4 bytes for uint
    }
    else if ( t.Equals(typeof(long)) ) {
      s.WriteByte((byte)'l');
      NumberSerializer.WriteLong((long)o,s);
      return 9; //1 typecode + 8 bytes for long 
    }
    else if ( t.Equals(typeof(ulong)) ) {
      s.WriteByte((byte)'L');
      NumberSerializer.WriteULong((ulong)o,s);
      return 9; //1 typecode + 8 bytes for ulong
    } 
    else if ( t.Equals(typeof(float)) ) {
      s.WriteByte((byte)'f');
      NumberSerializer.WriteFloat((float)o, s);
      return 5; //1 typecode + 4 bytes for float
    }
    else if ( o is Exception ) {
      AdrException ax = o as AdrException;
      if( ax == null ) {
        ax = new AdrException((Exception)o);
      }
      Hashtable xht = ax.ToHashtable();
      //Here is a map...
      int total_bytes = 2; //For the 'X' and 'x' bytes
      s.WriteByte((byte)'X'); //Start of map:
      IDictionaryEnumerator my_en = xht.GetEnumerator();
      while( my_en.MoveNext() ) {
	//Time for recursion:
        total_bytes += Serialize(my_en.Key, s);
        total_bytes += Serialize(my_en.Value, s);
      }
      s.WriteByte((byte)'x'); //End of map:
      return total_bytes;
    }
    else if ( t.IsArray ) {
      Type elt = t.GetElementType();
      ///@todo add more array serialization types here:
      if( elt.Equals(typeof(byte)) ||
          elt.Equals(typeof(int)) ) {
        return SerializeArray((Array)o, t, elt, s);
      }
      else {
        //All arrays are ILists, but this may take more space than the above
        return SerializeList( (IList)o, s );
      }
    }
    else  {
     if ( o is IList ) {
       return SerializeList( (IList)o, s);
     }
     else if ( o is IDictionary ) {
      IDictionary dict = o as IDictionary;
      //Here is a map...
      int total_bytes = 2; //For the '{' and '}' bytes
      s.WriteByte((byte)'{'); //Start of map:
      IDictionaryEnumerator my_en = dict.GetEnumerator();
      while( my_en.MoveNext() ) {
	//Time for recursion:
        total_bytes += Serialize(my_en.Key, s);
        total_bytes += Serialize(my_en.Value, s);
      }
      s.WriteByte((byte)'}'); //End of map:
      return total_bytes;
     }
     else {
      //This is not a supported type of object
      throw new Exception("Unsupported type: " + t.ToString());
     }
    }
  }

  /**
   * Boxing and Unboxing in .Net is a little tricky.  You can
   * only unbox directly to the correct type.  From that point,
   * you can use the normal type conversion.  More methods like
   * this will be handy when we start using generics
   */
  public static long UnboxToLong(object o) {
    Type t = o.GetType();
    long result = 0;
    if( t.Equals(typeof(byte))) {
      byte b = (byte)o;
      result = (long)b;
    }
    else
    if( t.Equals(typeof(sbyte))) {
      sbyte b = (sbyte)o;
      result = (long)b;
    }
    else
    if( t.Equals(typeof(ushort))) {
      ushort b = (ushort)o;
      result = (long)b;
    }
    else
    if( t.Equals(typeof(short))) {
      short b = (short)o;
      result = (long)b;
    }
    else
    if( t.Equals(typeof(uint))) {
      uint b = (uint)o;
      result = (long)b;
    }
    else
    if( t.Equals(typeof(int))) {
      int b = (int)o;
      result = (long)b;
    }
    else
    if( t.Equals(typeof(long))) {
      result = (long)o;
    }
    else
    if( t.Equals(typeof(ulong))) {
      ulong b = (ulong)o;
      result = (long)b;
    }
    else {
      throw new Exception("Cannot unbox, Unknown type: " + t.ToString());
    }
    return result;
  }

  protected static int SerializeArray(Array my_a, Type t, Type elt, Stream s)
  {
      s.WriteByte((byte)'a');
      int total_bytes = 1;
      if( my_a.Length <= Byte.MaxValue ) {
        //Length will fit in byte:
	byte l = (byte) my_a.Length;
	total_bytes += Serialize(l, s);
      }
      else if( my_a.Length <= UInt16.MaxValue ) {
	ushort l = (ushort)my_a.Length;
	total_bytes += Serialize(l, s);
      }
      else if( my_a.LongLength <= UInt32.MaxValue ) {
	uint l = (uint)my_a.Length;
	total_bytes += Serialize(l, s);
      }
      else {
        throw new Exception("Array too large: " + my_a.Length.ToString() );
      }
      if( elt.Equals(typeof(byte)) ) {
        //This is a byte array:
        s.WriteByte((byte)'B');
        total_bytes++;
        //Now write each byte:
        foreach(byte b in my_a) {
          s.WriteByte(b);
        }
        total_bytes += my_a.Length;
      }
      else if (elt.Equals(typeof(int))) {
        //This is a byte array:
        s.WriteByte((byte)'i');
        total_bytes++;
        //Now write each byte:
        foreach(int i in my_a) {
          NumberSerializer.WriteInt(i,s);
        }
        total_bytes += 4 * my_a.Length;
      }
      else {
        throw new Exception("Unsupported array type: " + elt.ToString() );
      }
      return total_bytes;

  }

  static protected int SerializeList(IList list, Stream s)
  {
    //Here is a list...
    int total_bytes = 2; //For the '(' and ')' bytes
    s.WriteByte((byte)'('); //Start of list, so lispy!:
    foreach(object it in list) {
      //Time for recursion:
      total_bytes += Serialize(it, s);
    }
    s.WriteByte((byte)')'); //end of list:
    return total_bytes;
  }
  
#if BRUNET_NUNIT
 [TestFixture]
 public class AdrTester {
  
  public bool ArrayEquals(Array a1, Array a2)
  {
    bool equals = true;
    equals = a1.Length == a2.Length;
    for(int i = 0; i < a1.Length; i++ ) {
      equals &= AdrEquals(a1.GetValue(i), a2.GetValue(i));
      if( !equals ) { break; }
    }
    return equals;
  }
  public bool IListEquals(IList l1, IList l2) {
    bool equals = true;
    bool end_1 = false;
    bool end_2 = false;
    //We don't know how long this thing is unfortunately
    int i = 0;
    try {
      for(i=0; i < Int32.MaxValue; i++) {
        equals &= AdrEquals(l1[i], l2[i]); 
        if( !equals ) { break; }
      }
    }
    catch(ArgumentOutOfRangeException) { }
    object o1 = null, o2 = null;
    try { //This should throw an exception:
      o1 = l1[i];
    }
    catch(ArgumentOutOfRangeException) { end_1 = true; }
    try { //This should throw an exception:
      o2 = l2[i];
    }
    catch(ArgumentOutOfRangeException) { end_2 = true; }
    if( o2 != null ) {
      equals &= o2.Equals(o1);
    }
    else {
      equals &= (o1 == null);
    }
    equals &= end_1 && end_2;
    return equals;
  }
  public bool DictEquals(IDictionary d1, IDictionary d2) {
    bool this_eq = true;
    IDictionaryEnumerator my_en = d1.GetEnumerator();
    while( my_en.MoveNext() ) {
      this_eq = this_eq && ( AdrEquals( d2[ my_en.Key ], my_en.Value ) );
      if( !this_eq ) { break; }
    }
    my_en = d2.GetEnumerator();
    while( my_en.MoveNext() ) {
      this_eq = this_eq && ( AdrEquals( d1[ my_en.Key ], my_en.Value ) );
      if( !this_eq ) { break; }
    }
    return this_eq;
  }
  public bool AdrEquals(object o1, object o2)
  {
    if( o1 == o2 ) { return true; }
    if( ( o1 == null ) || (o2 == null ) ) {
      //If both were null, we would have already returned true
      return false;
    }
    Type t1 = o1.GetType();
    Type t2 = o2.GetType();
    bool equals = t1.Equals( t2 );
    if( equals ) {
      if( t1.IsArray ) {
        return ArrayEquals((Array)o1,(Array)o2);
      }
      else if(o1 is IList) {
        return IListEquals((IList)o1,(IList)o2);
      }
      else if(o1 is IDictionary) {
        return DictEquals((IDictionary)o1, (IDictionary)o2);
      }
      else if( o1 is AdrException ) {
        //AdrExceptions are looser on what they consider equality
        return o1.Equals(o2);
      }
      else if( o2 is AdrException ) {
        //AdrExceptions are looser on what they consider equality
        return o2.Equals(o1);
      }
      else {
        return o1.Equals(o2) && o2.Equals(o1);
      }
    }
    else if( (o1 is Exception) || (o2 is Exception) ) {
      //The types don't match in this case
      if( o1 is AdrException ) {
        //AdrExceptions are looser on what they consider equality
        return o1.Equals(o2);
      }
      else if( o2 is AdrException ) {
        //AdrExceptions are looser on what they consider equality
        return o2.Equals(o1);
      }
      else {
        return o1.Equals(o2) && o2.Equals(o1);
      }
    }
    return equals;
  }
  protected void AssertSD(object o, string message)
  {
   try {
    MemoryStream ms = new MemoryStream();
    Serialize(o, ms);
    ms.Seek(0, SeekOrigin.Begin);
    object dso = Deserialize(ms);
    if( !AdrEquals(o, dso ) ) {
      Console.Error.WriteLine("{0} != {1}\n", o, dso);
    }
    Assert.IsTrue( AdrEquals(o, dso), message );
   }
   catch(Exception x) {
     Console.Error.WriteLine("{0}: {1}", message, x);
     Assert.IsTrue(false, message);
   }
  }
  protected void AssertE(object o, byte[] data, string message) {
    if( !(o is IDictionary) && !(o is Exception) ) {
      //The above types won't encode to be byte for byte identical
      MemoryStream ms = new MemoryStream();
      Serialize(o, ms);
      byte[] bin = ms.ToArray();
      if( ! AdrEquals(bin, data) ) { 
        Console.Error.WriteLine("{0} != {1}", bin, data);
      }
      Assert.IsTrue( AdrEquals(bin, data), "Encoding match: " + message);
    }
    object dso = Deserialize(new MemoryStream(data));
    if( ! AdrEquals(o, dso) ) { 
      Console.Error.WriteLine("{0} != {1}", o, dso);
    }
    Assert.IsTrue( AdrEquals(o, dso), "Decoding match: " + message);
  }
  /**
   * This is just a method which executes some tests:
   */
  [Test]
  public void Test() {

    //Here are some hand constructed examples:
    byte[] hand_test = new byte[]{(byte)'_', (byte)'H', (byte)'e', (byte)'y',0};
    AssertE("Hey", hand_test, "string");
    hand_test = new byte[]{(byte)'0'};
    AssertE(null, hand_test, "null");
    hand_test = new byte[]{(byte)'T'};
    AssertE(true, hand_test,"true");
    hand_test = new byte[]{(byte)'F'};
    AssertE(false, hand_test,"true");
    hand_test = new byte[]{ (byte)'B', (byte)128 };
    AssertE((byte)128, hand_test, "byte");
    hand_test = new byte[]{ (byte)'i', 0, 128, 0, 128 };
    int t_val = 128;
    t_val <<= 16;
    t_val += 128;
    AssertE(t_val, hand_test, "integer");
    hand_test = new byte[]{ (byte)'a', (byte)'B', 2, (byte)'i', 0,0,0,128, 0,0,0,55};
    AssertE(new int[]{128, 55}, hand_test, "int array");
    hand_test = new byte[]{ (byte)'X',
                            (byte)'_', (byte)'c', (byte)'o', (byte)'d', (byte)'e', 0,
                            (byte)'i',0,0,0,128,
                            (byte)'_',(byte)'m',(byte)'e',(byte)'s',(byte)'s',(byte)'a',(byte)'g',(byte)'e', 0,
                            (byte)'_', (byte)'n', (byte)'o', (byte)'!', 0,
                            (byte)'x' };
    AssertE(new AdrException(128, "no!"), hand_test, "exception hand test");
    //Lets do some round tripping:
    AssertSD(true, "bool true");
    AssertSD(false, "bool false");
    AssertSD(null, "null");
    AssertSD("test string", "string");
    AssertSD((sbyte)(-42),"sbyte");
    AssertSD((byte)42, "byte");
    AssertSD((short)(-4242), "short");
    AssertSD((ushort)(4242), "ushort");
    AssertSD((int)(-424242), "int");
    AssertSD((uint)(424242), "uint");
    AssertSD((long)(-424242424242), "long");
    AssertSD((ulong)(424242424242), "ulong");
    AssertSD(new AdrException(42, "something bad"), "exception");
    AssertSD(new Exception("standard exception"), "std. exception");
    try {
      //Stack traces are set by throw
      throw new AdrException(0,"test with Stack");
    }
    catch(Exception x) {
      AssertSD(x, "AdrException with stacktrace");
      //Console.Error.WriteLine(new AdrException(x));
    }
    try {
      //Stack traces are set by throw
      throw new Exception("test base with Stack");
    }
    catch(Exception x) {
      AssertSD(x, "exception with stacktrace");
      //Console.Error.WriteLine(new AdrException(x));
    }
    //Here is a list:
    ArrayList l = new ArrayList();
    l.Add(1); l.Add("hello"); l.Add("world"); l.Add(true); l.Add(-1); l.Add((short)100);
    l.Add(null);
    l.Add((ushort)10);
    l.Add((uint)2);
    AssertSD(l, "shallow list");
    //Now lets put some more complex stuff inside:
    byte[] byte_array = new byte[10];
    l.Add(byte_array);
    ArrayList list2 = new ArrayList();
    list2.Add("inside");
    l.Add( list2 );
    Hashtable ht = new Hashtable();
    ht["key0"] = "value0";
    ht["key1"] = "value1";
    //Lets check the hash table:
    AssertSD(ht, "shallow hash table");
    l.Add(ht);
    AssertSD(l, "list of array and hash");
    Random r = new Random();
    //Some random data tests:
    for( int i = 0; i< 100; i++ ) {
      int len = r.Next(1000);
      byte[] test = new byte[len];
      r.NextBytes(test);
      AssertSD(test, "byte array");
    }
    for( int i = 0; i < 100; i++) {
      int[] test = new int[ r.Next(1000) ];
      for(int j = 0; j < test.Length; j++) {
        test[j] = r.Next();
      }
      AssertSD(test, "int array");
    }
    //Here is a hashtable with a list and a hashtable:
    Hashtable ht2 = (Hashtable)ht.Clone();
    ht2["list"] = l;
    ht2["hash"] = ht;
    ht2[ 12 ] = "twelve";
    AssertSD(ht2, "hash of list and hash");

  }//End of Test()
 } //End of AdrTester()
#endif
}

}
