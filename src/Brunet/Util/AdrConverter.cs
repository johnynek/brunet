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
//#define PROFILE_ADR

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif


namespace Brunet.Util {
        
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
  public override string ToString() {
    return String.Format("{0}\nError Code: {1}", base.ToString(), this.Code);
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
 * This takes an object and defers serializing it until CopyTo is
 * called.  This avoid some memory usage by avoiding intermediate
 * buffers for objects sent over Edges.
 */
public class AdrCopyable : ICopyable {
  /**
   * The underlying object we will CopyTo a buffer
   */
  protected readonly object Obj;
  //We use this to write into to check size
  protected static readonly byte[] JUNK_BUFFER = new byte[1 << 17];

  public AdrCopyable(object o) {
    Obj = o;
  }
  
  /**
   * serialize the object and copy it into the buffer
   */
  public int CopyTo(byte[] buffer, int off) {
    return AdrConverter.Serialize(Obj, buffer, off);
  }

  /**
   * This is expensive, it requires us to serialize the object and see
   * how long it is.
   */
  public int Length {
    get {
      try {
        return AdrConverter.Serialize(Obj, JUNK_BUFFER, 0);
      }
      catch {
        //I guess the buffer was too short
        using(MemoryStream s = new MemoryStream() ) {
          return AdrConverter.Serialize( Obj, s );
        }
      }
    }
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

  /*
   * Here are some constants used in the code:
   */
  //Constants:
  protected const byte NULL = (byte)'0';
  protected const byte TRUE = (byte)'T';
  protected const byte FALSE = (byte)'F';
  //Integral types (fixed size):
  protected const byte SBYTE = (byte)'b';
  protected const byte BYTE = (byte)'B';
  protected const byte SHORT = (byte)'s';
  protected const byte USHORT = (byte)'S';
  protected const byte INT = (byte)'i';
  protected const byte UINT = (byte)'I';
  protected const byte LONG = (byte)'l';
  protected const byte ULONG = (byte)'L';
  //non-integral:
  protected const byte FLOAT = (byte)'f';
  protected const byte DOUBLE = (byte) 'd';
  //For arrays of numerical (fixed length) types:
  protected const byte ARRAY = (byte)'a';
  //delimited types:
  protected const byte STRING_S = (byte)'_';
  protected const byte STRING_E = (byte)0;
  protected const byte LIST_S = (byte)'(';
  protected const byte LIST_E = (byte)')';
  protected const byte MAP_S = (byte)'{';
  protected const byte MAP_E = (byte)'}';
  protected const byte EXCEPTION_S = (byte)'X';
  protected const byte EXCEPTION_E = (byte)'x';

#if PROFILE_ADR
  public static Dictionary<string,int> ADR_COUNT = new Dictionary<string,int>();
#endif
  protected readonly static Dictionary<System.Type, byte> _type_map;
  static AdrConverter() {
    _type_map = new Dictionary<System.Type, byte>();
    //String:
    _type_map[ typeof(string) ] = STRING_S;
    //Lists:
    _type_map[ typeof(ArrayList) ] = LIST_S;
    _type_map[ typeof(string[]) ] = LIST_S;
    _type_map[ typeof(object[]) ] = LIST_S;
    //Maps:
    _type_map[ typeof( System.Collections.Specialized.ListDictionary ) ] = MAP_S;
    _type_map[ typeof( Hashtable ) ] = MAP_S;
  }

  public static object Deserialize(Stream s) {
    bool finished = false;
    return Deserialize(s, NULL, out finished);
  }

  public static object Deserialize(MemBlock mb) {
    int size;
    bool fin;
    return Deserialize(mb, 0, NULL, out fin, out size);
  }
  public static object Deserialize(MemBlock mb, out int size) {
    bool fin;
    return Deserialize(mb, 0, NULL, out fin, out size);
  }
  public static object Deserialize(MemBlock mb, int offset, out int size) {
    bool fin;
    return Deserialize(mb, offset, NULL, out fin, out size);
  }
  private static object Deserialize(MemBlock b, int offset, byte term,
                                    out bool finished, out int size) {
    byte typecode = b[offset];
    finished = false;  //By default, we're not finished
    switch( typecode ) {
      case STRING_S:
        int count;
        string s = NumberSerializer.ReadString(b, offset + 1, out count);
        size = count + 1; //add one for the STRING_S byte
        return s;
      case LIST_S:
        IList lst = new ArrayList();
        bool lfin;
        int lsize;
        size = 1;
        offset++; //Skip the LIST_S
        do {
          object o = Deserialize(b, offset, LIST_E, out lfin, out lsize);
	  if( !lfin ) {
	    //If we are finished, tmp holds a meaningless null
	    lst.Add(o);
	  }
          offset += lsize;
          size += lsize;
        } while( ! lfin );
        return lst;
      case LIST_E:
        if( term == LIST_E ) { finished = true; size = 1; }
        else {
          throw new Exception(
               String.Format("terminator mismatch: found: {0} expected: {1}", term, LIST_E));
        }
        return null;
      case MAP_S:
        //Start of a map:
        IDictionary dresult = new Hashtable();
        bool mfinished = false;
        int msize;
        size = 1;
        offset++;
        do {
          //Magical recursion strikes again
          object key = Deserialize(b, offset, MAP_E, out mfinished, out msize);
          offset += msize;
          size += msize;
          if( !mfinished ) {
            object valu = Deserialize(b, offset, out msize);
            offset += msize;
            size += msize;
            dresult.Add(key, valu);
          }
        } while (false == mfinished);
        return dresult;
      case MAP_E:
        //End of a map:
        if (term == MAP_E) {
          //We were reading a list and now we are done:
          finished = true;
          size = 1;
        }
        else {
          throw new Exception(
               String.Format("terminator mismatch: found: {0} expected: {1}", term, MAP_E));
        }
        return null;
      case TRUE:
        size = 1;
        return true;
      case FALSE:
        size = 1;
        return false;
      case NULL:
        size = 1;
        return null;
      case SBYTE:
        size = 2;
        return (sbyte)b[offset + 1];
      case BYTE:
        size = 2;
        return b[offset + 1];
      case SHORT:
        size = 3;
        return NumberSerializer.ReadShort(b, offset + 1);
      case USHORT:
        size = 3;
        return (ushort)NumberSerializer.ReadShort(b, offset + 1);
      case INT:
        size = 5;
        return NumberSerializer.ReadInt(b, offset + 1);
      case UINT:
        size = 5;
        return (uint)NumberSerializer.ReadInt(b, offset + 1);
      case LONG:
        size = 9;
        return NumberSerializer.ReadLong(b, offset + 1);
      case ULONG:
        size = 9;
        return (ulong)NumberSerializer.ReadLong(b, offset + 1);
      case FLOAT:
        size = 5;
        return NumberSerializer.ReadFloat(b, offset + 1);
      case DOUBLE:
	size = 9;
	return NumberSerializer.ReadDouble(b, offset + 1);
      case EXCEPTION_S:
        //Start of a map:
        Hashtable eresult = new Hashtable();
        bool efinished = false;
        int esize;
        size = 1;
        offset++;
        do {
          //Magical recursion strikes again
          object key = Deserialize(b, offset, EXCEPTION_E, out efinished, out esize);
          offset += esize;
          size += esize;
          if( !efinished ) {
            object valu = Deserialize(b, offset, out esize);
            offset += esize;
            size += esize;
            eresult.Add(key, valu);
          }
        } while (false == efinished);
        return new AdrException(eresult);
      case EXCEPTION_E:
        //End of a map:
        if (term == EXCEPTION_E) {
          //We were reading a list and now we are done:
          finished = true;
          size = 1;
        }
        else {
          throw new Exception(
               String.Format("terminator mismatch: found: {0} expected: {1}",
                             term, EXCEPTION_E));
        }
        return null;
      case ARRAY:
        //Read length:
        int asize;
        object olength = Deserialize(b, offset + 1, out asize);
        //Due to boxing here, we have to be careful about unboxing,
        //this will get easier with generics:
        int length = (int)UnboxToLong(olength); 
        offset += 1 + asize;
	byte atype = b[offset];
        offset++;
	switch (atype) {
          case BYTE:
            byte[] aBresult = new byte[length];
            MemBlock b_a = b.Slice(offset, length);
            b_a.CopyTo(aBresult, 0);
            size = 1 + asize + 1 + length;
            return aBresult;
          case INT:
            int[] airesult = new int[length];
            for(int i = 0; i < airesult.Length; i++) {
              airesult[i] = NumberSerializer.ReadInt(b, offset);
              offset += 4;
            }
            size = 1 + asize + 1 + 4 * length;
            return airesult;
            ///@todo add more array types
          default:
            throw new Exception("Unsupported array type code: " + atype);
	}
      default:
        throw new Exception(String.Format("Unrecognized type code: {0}", typecode));
    }
  }
  /*
   * This is how the above is implemented to support recursion
   */
  private static object Deserialize(Stream s, byte terminator, out bool finished ) {
  
    int type = s.ReadByte();
    object result = null;
    finished = false; //Set the default value
    if( type < 0 ) {
      //This is an error
      throw new Exception("End of stream");
    }
    else {
      byte typecode = (byte)type;
      switch( typecode ) {
	case STRING_S:
	  //UTF-8 String:
          int bytelength = 0;
	  result = NumberSerializer.ReadString(s, out bytelength);
          break;
	case LIST_S:
	  //Start of a list:
 	  IList listresult = new ArrayList();
	  bool lfinished = false;
          do {
	    //The magic of recursion.
            object tmp = AdrConverter.Deserialize(s, LIST_E, out lfinished);
	    if( !lfinished ) {
	      //If we are finished, tmp holds a meaningless null
	      listresult.Add(tmp);
	    }
	  } while( false == lfinished );
          result = listresult;
          break;
	case LIST_E:
          //End of the list:
	  if (terminator == LIST_E) {
            //We were reading a list and now we are done:
	    finished = true;
	  }
	  else {
            throw new Exception("Unexpected terminator: ) != " + terminator);
	  }
	  result = null;
	  break;
	case MAP_S:
	  //Start of a map:
	  IDictionary dresult = new Hashtable();
	  bool mfinished = false;
	  do {
	    //Magical recursion strikes again
            object key = Deserialize(s, MAP_E, out mfinished);
	    if( !mfinished ) {
              object valu = Deserialize(s);
	      dresult.Add(key, valu);
	    }
	  } while (false == mfinished);
          result = dresult;
	  break;
	case MAP_E:
	  //End of a map:
	  if (terminator == MAP_E) {
            //We were reading a list and now we are done:
	    finished = true;
	  }
	  else {
            throw new Exception("Unexpected terminator: } != " + terminator);
	  }
	  result = null;
	  break;
        case TRUE:
          result = true;
          break;
        case FALSE:
          result = false;
          break;
	case NULL:
	  //Null:
	  result = null;
	  break;
	case SBYTE:
	  //Signed byte
	  int valy = s.ReadByte();
	  if( valy < 0 ) { throw new Exception("End of stream"); }
	  result = (sbyte)valy;
	  break;
	case BYTE:
	  //Unsigned byte
	  int valY = s.ReadByte();
	  if( valY < 0 ) { throw new Exception("End of stream"); }
	  result = (byte)valY;
	  break;
        case SHORT:
	  //signed short:
	  result = NumberSerializer.ReadShort(s);
	  break;
        case USHORT:
	  //unsigned short:
	  result = unchecked( (ushort)NumberSerializer.ReadShort(s) );
	  break;
        case INT:
	  //signed int:
	  result = NumberSerializer.ReadInt(s);
	  break;
        case UINT:
	  //unsigned int:
	  result = unchecked( (uint)NumberSerializer.ReadInt(s) );
	  break;
        case LONG:
          //signed long:
          result = NumberSerializer.ReadLong(s);
          break;
        case ULONG:
          //signed long:
          result = unchecked((ulong)NumberSerializer.ReadLong(s));
          break;
        case FLOAT:
	  //floating-point number
	  result = NumberSerializer.ReadFloat(s);
	  break;
        case DOUBLE:
	  //double-precision
	  result = NumberSerializer.ReadDouble(s);
	  break;
	case EXCEPTION_S:
	  //Start of an exception:
	  Hashtable xht = new Hashtable();
	  bool xfinished = false;
	  do {
	    //Magical recursion strikes again
            object key = Deserialize(s, EXCEPTION_E, out xfinished);
	    if( !xfinished ) {
              object valu = Deserialize(s);
	      xht.Add(key, valu);
	    }
	  } while (false == xfinished);
          result = new AdrException(xht);
	  break;
	case EXCEPTION_E:
	  //End of an exception:
	  if (terminator == EXCEPTION_E) {
            //We were reading a list and now we are done:
	    finished = true;
	  }
	  else {
            throw new Exception("Unexpected terminator: x != " + terminator);
	  }
	  result = null;
	  break;
	case ARRAY:
	  //Array:
	  //Read the length:
	  object olength = Deserialize(s);
          //Due to boxing here, we have to be careful about unboxing,
          //this will get easier with generics:
          long length = UnboxToLong(olength); 
	  int typec = s.ReadByte();
	  if ( typec < 0 ) { throw new Exception("Could not read array type"); }
	  byte atype = (byte)typec;
	  switch (atype) {
              case BYTE:
	        //unsigned byte:
                byte[] aBresult = new byte[length];
                int read_b = s.Read(aBresult, 0, (int)length);
                if( read_b != length ) {
                  throw new Exception("Could not read byte for array"); 
                }
                result = aBresult;
                break;
              case INT:
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
        default:
          throw new Exception("Unexcepted typecode: " + typecode);
      }
      return result;
    }

  }
  
  /** Serialize directly into the given byte array
   * @param o the object to serialize
   * @param dest the array to write into
   * @param offset the position to start at
   * Note, if this fails (throws an exception), it will still
   * modify the destination array
   */
  public static int Serialize(object o, byte[] dest, int offset) {
#if PROFILE_ADR
      string key;
      if( o == null ) {
        key = "<NULL>";
      }
      else {
        key = o.GetType().ToString();
      }
    lock(ADR_COUNT) {
      if( ADR_COUNT.ContainsKey(key) ) {
        ADR_COUNT[key] = ADR_COUNT[key] + 1;
      }
      else {
        ADR_COUNT[key] = 1;
      }
    }
#endif
    if( o == null ) {
      //Not much work to do:
      dest[offset] = NULL;
      return 1; //1 byte for null
    }
    if(o is string) {
      dest[offset] = STRING_S;
      string val = (string)o;
      int bytes = NumberSerializer.WriteString(val, dest, offset + 1);
      return 1 + bytes; //the typecode + the serialized string
    }
    
    //Else, o is some kind of object:
    /*
     * We put the most commonly used types first so we don't have to go
     * through a huge list everytime we serialize
     */
    System.Type t = o.GetType();
    byte this_type;
    //The hashtable allows us to be faster on the several types
    //of lists and dictionaries that will be handled by the same code:
    if( _type_map.TryGetValue(t, out this_type) ) {
      if( this_type == LIST_S ) {
        return SerializeList( (IList)o, dest, offset );
      }
      else
      if( this_type == MAP_S ) {
        return SerializeDict( (IDictionary)o, dest, offset);
      }
    }
    //Else, we are dealing with a less common type:

    if ( t.IsArray ) {
      Type elt = t.GetElementType();
      ///@todo add more array serialization types here:
      if( elt.Equals(typeof(byte)) ||
          elt.Equals(typeof(int)) ) {
        return SerializeArray((Array)o, t, elt, dest, offset);
      }
      else {
        //All arrays are ILists, but this may take more space than the above
        return SerializeList( (IList)o, dest, offset );
      }
    }
    else if ( o is IList ) {
      return SerializeList( (IList)o, dest, offset );
    }
    else if ( o is IDictionary ) {
      return SerializeDict( (IDictionary)o, dest, offset);
    }
    else
    if( t.Equals( typeof(bool) ) ) {
      //boolean value:
      bool b = (bool)o;
      if( b ) { dest[offset] = TRUE; }
      else { dest[offset] = FALSE; }
      return 1;
    }
    else
    if ( t.Equals(typeof(byte)) ) {
      //Unsigned byte
      dest[offset] = BYTE;
      dest[offset + 1] = (byte)o;
      return 2;
    }
    else if ( t.Equals(typeof(sbyte)) ) {
      dest[offset] = SBYTE;
      //Unbox:
      sbyte v = (sbyte)o;
      //Convert:
      dest[offset + 1] = (byte)v;
      return 2;
    }
    else if ( t.Equals(typeof(short)) ) {
      dest[offset] = SHORT;
      NumberSerializer.WriteShort((short)o,dest, offset + 1);
      return 3; //1 typecode + 2 bytes for short
    }
    else if ( t.Equals(typeof(ushort)) ) {
      dest[offset] = USHORT;
      NumberSerializer.WriteUShort((ushort)o, dest, offset + 1);
      return 3; //1 typecode + 2 bytes for short
    }
    else if ( t.Equals(typeof(int)) ) {
      dest[offset]=INT;
      NumberSerializer.WriteInt((int)o,dest,offset+1);
      return 5; //1 typecode + 4 bytes for int 
    }
    else if ( t.Equals(typeof(uint)) ) {
      dest[offset]=UINT;
      NumberSerializer.WriteUInt((uint)o,dest, offset + 1);
      return 5; //1 typecode + 4 bytes for uint
    }
    else if ( t.Equals(typeof(long)) ) {
      dest[offset]=LONG;
      NumberSerializer.WriteLong((long)o,dest, offset + 1);
      return 9; //1 typecode + 8 bytes for long 
    }
    else if ( t.Equals(typeof(ulong)) ) {
      dest[offset]=ULONG;
      //Unbox
      ulong ulv = (ulong)o;
      //Convert:
      long lv = (long)ulv;
      NumberSerializer.WriteLong(lv, dest, offset + 1);
      return 9; //1 typecode + 8 bytes for ulong
    } 
    else if ( t.Equals(typeof(float)) ) {
      dest[offset]=FLOAT;
      NumberSerializer.WriteFloat((float)o, dest, offset + 1);
      return 5; //1 typecode + 4 bytes for float
    }
    else if ( t.Equals(typeof(double)) ) {
      dest[offset]=DOUBLE;
      NumberSerializer.WriteDouble((double)o, dest, offset + 1);
      return 9; // 1 typecode + 8 bytes for double 
    }
    else if ( o is Exception ) {
      int orig = offset;
      AdrException ax = o as AdrException;
      if( ax == null ) {
        ax = new AdrException((Exception)o);
      }
      Hashtable xht = ax.ToHashtable();
      //Here is a map...
      dest[offset]=EXCEPTION_S;
      offset += 1;
      IDictionaryEnumerator my_en = xht.GetEnumerator();
      while( my_en.MoveNext() ) {
	//Time for recursion:
        offset += Serialize(my_en.Key, dest, offset);
        offset += Serialize(my_en.Value, dest, offset);
      }
      dest[offset]=EXCEPTION_E;
      offset += 1;
      return offset - orig;
    }
    else if ( o is MemBlock ) {
      //Just serialize this as a byte array
      int orig = offset;
      MemBlock d = (MemBlock)o;
      dest[offset] = ARRAY;
      offset += 1;
      int length = d.Length;
      offset += SerializePosNum((ulong)length, dest, offset);
      dest[offset] = BYTE;
      offset += 1;
      offset += d.CopyTo(dest, offset);
      return offset - orig;
    }
    else
    {
     //This is not a supported type of object
     throw new Exception("Unsupported type: " + t.ToString());
    }
  }
  /**
   * @return the number of bytes written into the stream
   */
  public static int Serialize(object o, Stream s) {
    if( o == null ) {
      //Not much work to do:
      s.WriteByte(NULL);
      return 1; //1 byte for null
    }
    
    //Else, o is some kind of object:
    /*
     * We put the most commonly used types first so we don't have to go
     * through a huge list everytime we serialize
     */
    System.Type t = o.GetType();
    if ( t.Equals(typeof(string)) ) {
      s.WriteByte(STRING_S);
      string val = (string)o;
      int bytes = NumberSerializer.WriteString(val, s);
      return 1 + bytes; //the typecode + the serialized string
    }
    else
    if ( t.IsArray ) {
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
    else if ( o is IList ) {
      return SerializeList( (IList)o, s);
    }
    else if ( o is IDictionary ) {
     IDictionary dict = o as IDictionary;
     //Here is a map...
     int total_bytes = 2; //For the '{' and '}' bytes
     s.WriteByte(MAP_S); //Start of map:
     IDictionaryEnumerator my_en = dict.GetEnumerator();
     while( my_en.MoveNext() ) {
	//Time for recursion:
       total_bytes += Serialize(my_en.Key, s);
       total_bytes += Serialize(my_en.Value, s);
     }
     s.WriteByte(MAP_E); //End of map:
     return total_bytes;
    }
    else
    if( t.Equals( typeof(bool) ) ) {
      //boolean value:
      bool b = (bool)o;
      if( b ) { s.WriteByte(TRUE); }
      else { s.WriteByte(FALSE); }
      return 1;
    }
    else
    if ( t.Equals(typeof(byte)) ) {
      //Unsigned byte
      s.WriteByte(BYTE);
      s.WriteByte((byte)o);
      return 2;
    }
    else if ( t.Equals(typeof(sbyte)) ) {
      s.WriteByte(SBYTE);
      long v = UnboxToLong(o);
      s.WriteByte((byte)v);
      return 2;
    }
    else if ( t.Equals(typeof(short)) ) {
      s.WriteByte(SHORT);
      NumberSerializer.WriteShort((short)o,s);
      return 3; //1 typecode + 2 bytes for short
    }
    else if ( t.Equals(typeof(ushort)) ) {
      s.WriteByte(USHORT);
      NumberSerializer.WriteUShort((ushort)o,s);
      return 3; //1 typecode + 2 bytes for short
    }
    else if ( t.Equals(typeof(int)) ) {
      s.WriteByte(INT);
      NumberSerializer.WriteInt((int)o,s);
      return 5; //1 typecode + 4 bytes for int 
    }
    else if ( t.Equals(typeof(uint)) ) {
      s.WriteByte(UINT);
      NumberSerializer.WriteUInt((uint)o,s);
      return 5; //1 typecode + 4 bytes for uint
    }
    else if ( t.Equals(typeof(long)) ) {
      s.WriteByte(LONG);
      NumberSerializer.WriteLong((long)o,s);
      return 9; //1 typecode + 8 bytes for long 
    }
    else if ( t.Equals(typeof(ulong)) ) {
      s.WriteByte(ULONG);
      NumberSerializer.WriteULong((ulong)o,s);
      return 9; //1 typecode + 8 bytes for ulong
    } 
    else if ( t.Equals(typeof(float)) ) {
      s.WriteByte(FLOAT);
      NumberSerializer.WriteFloat((float)o, s);
      return 5; //1 typecode + 4 bytes for float
    }
    else if ( t.Equals(typeof(double)) ) {
      s.WriteByte(DOUBLE);
      NumberSerializer.WriteDouble((double)o, s);
      return 9; // 1 typecode + 8 bytes for double 
    }
    else if ( o is Exception ) {
      AdrException ax = o as AdrException;
      if( ax == null ) {
        ax = new AdrException((Exception)o);
      }
      Hashtable xht = ax.ToHashtable();
      //Here is a map...
      int total_bytes = 2; //For the 'X' and 'x' bytes
      s.WriteByte(EXCEPTION_S);
      IDictionaryEnumerator my_en = xht.GetEnumerator();
      while( my_en.MoveNext() ) {
	//Time for recursion:
        total_bytes += Serialize(my_en.Key, s);
        total_bytes += Serialize(my_en.Value, s);
      }
      s.WriteByte(EXCEPTION_E);
      return total_bytes;
    }
    else if ( o is MemBlock ) {
      //Just serialize this as a byte array
      MemBlock d = (MemBlock)o;
      s.WriteByte(ARRAY);
      int total_bytes = 1;
      total_bytes += SerializePosNum( (ulong)d.Length, s );
      //This is a byte array:
      s.WriteByte(BYTE);
      total_bytes++;
      //Now write each byte:
      d.WriteTo(s);
      total_bytes += d.Length;
      return total_bytes;
    }
    else
    {
     //This is not a supported type of object
     throw new Exception("Unsupported type: " + t.ToString());
    }
  }

  protected static int SerializeDict(IDictionary dict, byte[] dest, int offset) {
     int orig_off = offset;
     //Here is a map...
     dest[offset] = MAP_S;
     offset += 1;
     IDictionaryEnumerator my_en = dict.GetEnumerator();
     while( my_en.MoveNext() ) {
	//Time for recursion:
       offset += Serialize(my_en.Key, dest, offset);
       offset += Serialize(my_en.Value, dest, offset);
     }
     dest[offset] = MAP_E;
     offset += 1;
     return offset - orig_off;
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
  /**
   * Serialize as smallest type that will hold the value
   */
  protected static int SerializePosNum(ulong v, Stream s) {
      int total_bytes = 0;
      if( v <= Byte.MaxValue ) {
        //Length will fit in byte:
	byte l = (byte) v;
	total_bytes += Serialize(l, s);
      }
      else if( v <= UInt16.MaxValue ) {
	ushort l = (ushort)v;
	total_bytes += Serialize(l, s);
      }
      else if( v <= UInt32.MaxValue ) {
	uint l = (uint)v;
	total_bytes += Serialize(l, s);
      }
      else {
        throw new Exception("Number too large: " + v.ToString() );
      }
      return total_bytes;
  }
  protected static int SerializePosNum(ulong v, byte[] dest, int off) {
      if( v <= Byte.MaxValue ) {
        dest[off] = BYTE;
        dest[off + 1] = (byte)v;
        return 2;
      }
      else if( v <= UInt16.MaxValue ) {
        dest[off] = USHORT;
        NumberSerializer.WriteUShort((ushort)v, dest, off + 1);
        return 3;
      }
      else if( v <= UInt32.MaxValue ) {
        dest[off] = UINT;
        NumberSerializer.WriteUInt((uint)v, dest, off + 1);
        return 5;
      }
      else {
        throw new Exception("Number too large: " + v.ToString() );
      }
  }
  protected static int SerializeArray(Array my_a, Type t, Type elt, byte[] dest, int off)
  {
      int orig_offset = off;
      dest[off] = ARRAY;
      off += 1;
      //Write the length
      int length = my_a.Length;
      off += SerializePosNum((ulong)length, dest, off);
      if( elt.Equals(typeof(byte)) ) {
        //This is a byte array:
        dest[off] = BYTE;
        off += 1;
        Array.Copy(my_a, 0, dest, off, length);
        off += length;
      }
      else if (elt.Equals(typeof(int))) {
        //This is a byte array:
        dest[off] = INT;
        off += 1;
        //Now write each byte:
        foreach(int i in my_a) {
          NumberSerializer.WriteInt(i, dest, off);
          off += 4;
        }
      }
      else {
        throw new Exception("Unsupported array type: " + elt.ToString() );
      }
      return off - orig_offset;

  }
  protected static int SerializeArray(Array my_a, Type t, Type elt, Stream s)
  {
      s.WriteByte(ARRAY);
      int total_bytes = 1;
      total_bytes += SerializePosNum( (ulong)my_a.LongLength, s );
      if( elt.Equals(typeof(byte)) ) {
        //This is a byte array:
        s.WriteByte(BYTE);
        total_bytes++;
        //Now write each byte:
        foreach(byte b in my_a) {
          s.WriteByte(b);
        }
        total_bytes += my_a.Length;
      }
      else if (elt.Equals(typeof(int))) {
        //This is a byte array:
        s.WriteByte(INT);
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
    s.WriteByte(LIST_S); //Start of list, so lispy!:
    int count = list.Count;
    for(int i = 0; i < count; i++) {
      //Time for recursion:
      total_bytes += Serialize(list[i], s);
    }
    s.WriteByte(LIST_E); //end of list:
    return total_bytes;
  }
  
  static protected int SerializeList(IList list, byte[] dest, int off)
  {
    int orig_off = off;
    //Here is a list...
    dest[off] = LIST_S; //Start of list, so lispy!:
    off += 1;
    int count = list.Count;
    for(int i = 0; i < count; i++) {
      //Time for recursion:
      off += Serialize(list[i], dest, off);
    }
    dest[off] = LIST_E; //end of list:
    return off + 1 - orig_off;
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
    bool equals = (l1.Count == l2.Count);
    for(int i = 0; i < l1.Count; i++) {
      equals &= AdrEquals(l1[i], l2[i]);
    }
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
    if( o1 is MemBlock ) {
      return o1.Equals(o2);
    }
    else if( o2 is MemBlock ) {
      return o2.Equals(o1);
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
  protected void AssertSD(object o, string message)
  {
   try {
    MemoryStream ms = new MemoryStream();
    int serialized = Serialize(o, ms);
    byte[] sbuf = new byte[serialized];
    int s2count = Serialize(o, sbuf, 0);
    Assert.AreEqual(serialized, s2count, String.Format("byte and stream length same: {0}", o));
    byte[] buf = ms.ToArray();
    for(int i = 0; i < Math.Max(s2count, serialized); i++) {
      Assert.AreEqual(buf[i], sbuf[i], String.Format("byte for byte comparison (stream vs. byte): {0}", o));
    }
    Assert.AreEqual(serialized, buf.Length, "Buffer length same as written");
    ms.Seek(0, SeekOrigin.Begin);
    object dso = Deserialize(ms);
    object dso2 = Deserialize(MemBlock.Reference(buf));
    object dso3 = Deserialize(MemBlock.Copy(new AdrCopyable(o)));
    if( !AdrEquals(o, dso ) ) {
      Console.Error.WriteLine("{0} != {1}\n", o, dso);
    }
    Assert.IsTrue( AdrEquals(o, dso), message );
    if( !AdrEquals(o, dso2 ) ) {
      Console.Error.WriteLine("{0} != {1}\n", o, dso2);
    }
    if( !AdrEquals(o, dso3 ) ) {
      Console.Error.WriteLine("{0} != {1}\n", o, dso2);
    }
    Assert.IsTrue( AdrEquals(o, dso2), message );
    Assert.IsTrue( AdrEquals(o, dso3), message );
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
    object dso2 = Deserialize(MemBlock.Reference(data));
    if( ! AdrEquals(o, dso) ) { 
      Console.Error.WriteLine("{0} != {1}", o, dso);
    }
    Assert.IsTrue( AdrEquals(o, dso), "Decoding match: " + message);
    if( ! AdrEquals(o, dso2) ) { 
      Console.Error.WriteLine("{0} != {1}", o, dso2);
    }
    Assert.IsTrue( AdrEquals(o, dso2), "Decoding match: " + message);
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
    AssertSD(new object[0], "zero length object array");
    AssertSD(new object[]{"string", (int)42, null}, "length 3 object array");
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
      MemBlock b = MemBlock.Reference(test);
      AssertSD(b, "MemBlock test");
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
