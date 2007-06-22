using System;
using System.Collections;
using System.Text;
using CookComputing.XmlRpc;

namespace Brunet {
  /**
   * This class works in a "try my best" manner in which it only converts types
   * that would work if conversion is applied. If the parameter is invalid in both
   * serializers or not possible to convert, then nothing happens and let the serializers
   * throw corresponding exceptions.
   * 
   * ArdConverter compatible types
   * string, Array, IList, IDictionary, bool, byte, sbyte, short, ushort, int, uint, long,
   * ulong, float, Exception, MemBlock, 
   * 
   * XmlRpcSerializer compatible types:
   * int, bool, string, double, DateTime, byte[](Base64), XmlRpcStruct, Array, int?, bool?,
   * DateTime?, void, (IsValueType && !IsPrimitive && IsEnum) || IsClass
   * @see Brunet.ArdConverter
   * @see XMLRPC.NET <http://www.xml-rpc.net>
   */
  public class AdrXmlRpcConverter {
    public static object Adr2XmlRpc(object o) {
      bool modified;
      return Adr2XmlRpc(o, out modified);
    }
    
    /**
     * This method converts the objects compatible in AdrConvertor to 
     * objects that compatible in XMLRPC.NET library.     
     * byte[], MemBlock -> string(base64)
     * IDictionary -> XmlRpcStruct
     * IList -> Array
     */
    public static object Adr2XmlRpc(object o, out bool modified) {
      if(o == null) {
        throw new ArgumentNullException();
      }

      object retval;

      System.Type t = o.GetType();
      if(t == typeof(byte[])) {
        /*
         * It's not an ideal conversion, this way we cannot use <base64>. However, XmlRpcSerializer
         * recognize byte[][] as a multi-dimension array
         */
        MemBlock mb = (MemBlock)o;
        retval = mb.ToBase64String();
        modified = true;
      } else if (t.IsArray){
        ArrayList list = new ArrayList((ICollection)o);
        bool m;
        modified = false;
        for (int i = 0; i < list.Count; i++) {
          list[i] = Adr2XmlRpc(list[i], out m);
          if (m == true) {
            modified = true;
          }
        }
        retval = list.ToArray();
      } else if (o is IDictionary) {
        modified = true;
        XmlRpcStruct xrs = new XmlRpcStruct();
        IDictionary dict = o as IDictionary;
        
        IDictionaryEnumerator my_en = dict.GetEnumerator();
        while (my_en.MoveNext()) {
          object key = Adr2XmlRpc(my_en.Key);
          /*
           * XmlRpcStruct requires keys to be strings, we just use ToString() to generate
           * strings.
           */          
          string str_key = key.ToString();
          object val = Adr2XmlRpc(my_en.Value);          
          xrs.Add(str_key, val);
        }
        retval = xrs;
      } else if(o is IList) {
        modified = true;  //list -> array
        ArrayList list = new ArrayList((ICollection)o);
        for (int i = 0; i < list.Count; i++) {
          list[i] = Adr2XmlRpc(list[i]);
        }
        retval = list.ToArray();
      } else if(o is MemBlock) {
        modified = true;
        MemBlock mb = (MemBlock)o;
        /*
         * Cam't convert it to byte[] 
         */
        retval = mb.ToBase64String();
      } else if(t == typeof(Single)) {
        retval = Convert.ToDouble(o);
        modified = true;
      } else if (t == typeof(short) || t == typeof(ushort)) {
        retval = Convert.ToInt32(o);
        modified = true;
      } else if (t == typeof(long) || t == typeof(ulong)) {
        retval = Convert.ToString(o);
        modified = true;
      } else if (t == typeof(uint) || t == typeof(byte) || t == typeof(sbyte)) {
        retval = Convert.ToInt32(o);
        modified = true;
      } else if(o is AdrException) {
        AdrException e = (AdrException)o;
        XmlRpcStruct xrs = new XmlRpcStruct();
        xrs.Add("code", e.Code);
        xrs.Add("message", e.Message);
        xrs.Add("stacktrace", e.StackTrace);
        retval = xrs;
        modified = true;
      } else if(o is Exception) {
        Exception e = (Exception)o;
        throw new Exception("XmlRpcConverter rethrowed Exception:" + e.Message);
      } else if(o is ISender) {
        ISender s = (ISender)o;
        retval = s.ToString();
        modified = true;
      } else {
        retval = o;
        modified = false;
      }      
      return retval;
    }

    public static object XmlRpc2Adr(object o) {
      bool modified;
      return XmlRpc2Adr(o, out modified);
    }
    
    /**
     * This method converts the objects compatible in XMLRPC.NET library to 
     * objects that compatible in AdrConvertor
     */
    public static object XmlRpc2Adr(object o, out bool modified) {
      if (o == null) {
        throw new ArgumentNullException();
      }
      object retval;

      System.Type t = o.GetType();
      if (t == typeof(byte[])) {
        byte[] b = (byte[])o;
        if (b.Length == 0) {
          //empty byte[] array -> null
          retval = null;
          modified = true;
        } else {
          retval = o;
          modified = false;
        }
      } else {
        retval = o;
        modified = false;
      }

      return retval;
    }

    /**
     * This is to explicitely tell the converter that not to merge the array
     * in any case
     * Usually used for the methods of a variable number of parameters
     */
    public static object[] XmlRpc2AdrParams(object[] oa) {
      if (oa == null) {
        throw new ArgumentNullException();
      }
      ArrayList args = new ArrayList((ICollection)oa);
      ArrayList ret = new ArrayList();
      if(oa.Length > 0) {
        object o = oa[0];
        if (o is string) {
          string s = (string)o;
          if (s.Equals("null")) {
            args.RemoveAt(0);
          }
        }
      }
      foreach (object o in args) {
        ret.Add(XmlRpc2Adr(o));
      }
      return ret.ToArray();
    }
  }
}
