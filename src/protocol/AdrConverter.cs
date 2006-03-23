using System;
using System.IO;
using System.Collections;

namespace Brunet {
        
public class AdrException : Exception {

  protected int _code;
  public int Code { get { return _code; } }

  public AdrException(int code, string message) : base(message) {
    _code = code;
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
        case 'b':
	  //Boolean:
	  result = NumberSerializer.ReadBool(s);
	  break;
	case '0':
	  //Null:
	  result = null;
	  break;
	case 'y':
	  //Signed byte
	  int valy = s.ReadByte();
	  if( valy < 0 ) { throw new Exception("End of stream"); }
	  result = (sbyte)valy;
	  break;
	case 'Y':
	  //Unsigned byte
	  int valY = s.ReadByte();
	  if( valY < 0 ) { throw new Exception("End of stream"); }
	  result = (byte)valY;
	  break;
        case 'h':
	  //signed short:
	  result = NumberSerializer.ReadShort(s);
	  break;
        case 'H':
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
	case 's':
	  //UTF-8 String:
          int bytelength = 0;
	  result = NumberSerializer.ReadString(s, out bytelength);
          break;
        case 'x':
          //This is a serialized exception, we don't throw it, we just return it
          int code = NumberSerializer.ReadInt(s);
          int meslen = 0;
	  string message = NumberSerializer.ReadString(s, out meslen);
          result = new AdrException(code, message);
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
              case 'Y':
	        //unsigned byte:
                byte[] aYresult = new byte[length];
                for(int i = 0; i < aYresult.Length; i++) {
                  int tempval = s.ReadByte();
	          if ( tempval < 0 ) { throw new Exception("Could not read byte for array"); }
                  aYresult[i] = (byte)tempval;
                }
                result = aYresult;
                break;
              case 'i':
	        //signed int:
                int[] airesult = new int[length];
                for(int i = 0; i < airesult.Length; i++) {
                  airesult[i] = NumberSerializer.ReadInt(s);
                }
                result = airesult;
                break;
                ///TODO add more array types
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
      s.WriteByte((byte)'b');
      bool b = (bool)o;
      if( b ) { s.WriteByte(1); }
      else { s.WriteByte(0); }
      return 2;
    }
    else if ( t.Equals(typeof(string)) ) {
      s.WriteByte((byte)'s');
      string val = (string)o;
      int bytes = NumberSerializer.WriteString(val, s);
      return 1 + bytes; //the typecode + the serialized string
    }
    else if ( t.Equals(typeof(byte)) ) {
      //Unsigned byte
      s.WriteByte((byte)'Y');
      s.WriteByte((byte)o);
      return 2;
    }
    else if ( t.Equals(typeof(sbyte)) ) {
      s.WriteByte((byte)'y');
      s.WriteByte(unchecked((byte)o));
      return 2;
    }
    else if ( t.Equals(typeof(short)) ) {
      s.WriteByte((byte)'h');
      NumberSerializer.WriteShort((short)o,s);
      return 3; //1 typecode + 2 bytes for short
    }
    else if ( t.Equals(typeof(ushort)) ) {
      s.WriteByte((byte)'H');
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
      return 5; //1 typecode + 2 bytes for int
    }
    else if ( o is Exception ) {
      //We are serializing an exception:
      AdrException ax = o as AdrException;
      int code = 0;
      string message;
      if( ax != null ) {
        code = ax.Code;
        message = ax.Message;
      }
      else {
        message = ((Exception)o).Message;
      }
      s.WriteByte((byte)'x');
      NumberSerializer.WriteInt(code, s);
      int bytes = NumberSerializer.WriteString(message, s);
      return 5 + bytes; //the typecode + int + the serialized string
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
        s.WriteByte((byte)'Y');
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
  
  /**
   * This is just a method which executes some tests:
   */
  public static void Test() {

    //Lets do some round tripping:
    ArrayList l = new ArrayList();
    l.Add(1); l.Add("hello"); l.Add("world"); l.Add(true); l.Add(-1); l.Add((short)100);
    l.Add(null);
    l.Add((ushort)10);
    l.Add((uint)2);
    byte[] byte_array = new byte[10];
    l.Add(byte_array);
    ArrayList list2 = new ArrayList();
    list2.Add("inside");
    l.Add( list2 );
    Hashtable ht = new Hashtable();
    ht["key0"] = "value0";
    ht["key1"] = "value1";
    l.Add(ht);
    //Exception x = new Exception("test exception");
    //l.Add(x);
    MemoryStream ms = new MemoryStream();
    Serialize(l, ms);
    /*
    System.IO.FileStream fs = new System.IO.FileStream("test",FileMode.Create);
    ms.WriteTo(fs);
    fs.Flush();
    fs.Close();
    */
    ms.Seek(0, SeekOrigin.Begin);
    object list = Deserialize(ms);
   
    bool list_eq = (l.Count == ((IList)list).Count);
    bool this_eq = false;
    for(int i = 0; i < l.Count; i++) {
      //Console.WriteLine("{0}",o);
      object it = ((IList)list)[i];
      if( l[i] == null ) {
        this_eq =  ( it == null );
      }
      else if ( l[i] is IList ) {
        //Check inside:
        IList orig = (IList)l[i];
        IList des = (IList)it;
        this_eq = orig.Count == des.Count;
	if( !this_eq ) { Console.WriteLine("inner list count not equal"); }
        for(int j = 0; j < orig.Count; j++) {
          this_eq = this_eq && (orig[j].Equals( des[j] ));
        }
      }
      else if (l[i] is IDictionary ) {
        IDictionary orig = (IDictionary)(l[i]);
        IDictionary des = (IDictionary)it;
        IDictionaryEnumerator my_en = orig.GetEnumerator();
        this_eq = true;
        while( my_en.MoveNext() ) {
          this_eq = this_eq && ( des[ my_en.Key ].Equals( my_en.Value ) );
        }
      }
      else {
        this_eq = l[i].Equals( it );
      }
      if( !this_eq ) {
        Console.WriteLine("l[{0}] = {1}\t list[{0}] = {2}",i, l[i], it);
      }
      list_eq = list_eq && this_eq;
    } 
    if( !list_eq ) { Console.WriteLine("Lists not equal"); }
    Random r = new Random();
    //Some random data tests:
    for( int i = 0; i< 1000; i++ ) {
      int len = r.Next(10000);
      byte[] test = new byte[len];
      r.NextBytes(test);
      ms.Seek(0, SeekOrigin.Begin);
      Serialize(test, ms);
      ms.Seek(0, SeekOrigin.Begin);
      object bindata = Deserialize(ms);
      byte[] test2 = (byte[])bindata;
      bool equal = test.Length == test.Length;
      for(int j = 0; j < test.Length; j++) {
        equal = equal && (test2[j] == test[j]);
      }
      if( equal ) {
        //Console.WriteLine("Successful byte array roundtrip");
      }
      else {
        Console.WriteLine("**Unsuccessful byte array roundtrip");
      }
    }

    
  }
	
}

}
