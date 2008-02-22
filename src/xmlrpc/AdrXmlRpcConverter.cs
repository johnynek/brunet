using System;
using System.Collections;
using System.Text;
using CookComputing.XmlRpc;

namespace Brunet.Rpc {
  /**
   * ArdConverter compatible types
   * string, Array, IList, IDictionary, bool, byte, sbyte, short, ushort, int, uint, long,
   * ulong, float, Exception, MemBlock, *null*
   * 
   * XmlRpcSerializer compatible types:
   * int, bool, string, double, DateTime, byte[](Base64), XmlRpcStruct, Array, 
   * int?, bool?, double?, DateTime?: when present in structs, they are omitted when null
   * void, 
   * *null* is converted to <string/>
   * (IsValueType && !IsPrimitive && !IsEnum) || IsClass) : members of this kind of types are 
   * extracted and checked, if they are among above types, then could be converted by XML-RPC
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
     * byte[], MemBlock -> byte[] -> string(base64)
     * other array -> array ( elements converted)
     * IDictionary -> XmlRpcStruct (key -> string)
     * IList -> array
     * float -> double
     * long, ulong -> string
     * AdrException -> XmlRpcFaultException (w/ errorCode added)
     * Exception -> Exception (converted by XmlRpcFaultException by xmlrpc.net)
     * ISender -> string (using ToString())
     * short, ushort, uint, byte, sbyte -> int
     * null -> string.Empty
     */
    public static object Adr2XmlRpc(object o, out bool modified) {
      object retval;
      if(o == null) {
        /*
         * If null is returned when the method is not recursively called by itself,
         * it is OK because XmlRpc.Net will convert it to string.Empty.
         * If not, the null element's outer data structure like Array and IDictionary,
         * which themselves allow null elements, might not be handled correctly: 
         * XmlRpc.Net can't serialize IDictionary and Array with null elements.
         * So we return s.Empty directly from here
         */
        retval = string.Empty;
        modified = true;
        return retval;
      }

      System.Type t = o.GetType();
      // byte arrays are converted to base64 strings in XmlRpc
      // so we treat it as a special case of array
      if(t == typeof(byte[])) {
        retval = o;
        modified = false;
      } 
      // convert each element
      else if (t.IsArray){
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
      }
      //IDictionary -> XmlRpcStruct (string key)
      else if (o is IDictionary) {
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
      } 
      //XmlRpcSerializer doesn't recognize lists
      //IList -> Array
      else if(o is IList) {
        modified = true;  //list -> array
        ArrayList list = new ArrayList((ICollection)o);
        for (int i = 0; i < list.Count; i++) {
          list[i] = Adr2XmlRpc(list[i]);
        }
        retval = list.ToArray();
      } 
      //Memblock -> byte[]
      else if(o is MemBlock) {
        modified = true;
        MemBlock mb = (MemBlock)o;
        byte[] b = new byte[mb.Length];
        mb.CopyTo(b, 0);
        retval = b;
      }
      //float -> double
      else if(t == typeof(Single)) {
        retval = Convert.ToDouble(o);
        modified = true;
      }
      else if (t == typeof(short) || t == typeof(ushort) || t == typeof(uint) || t == typeof(byte) || t == typeof(sbyte)) {
        retval = Convert.ToInt32(o);
        modified = true;
      } 
      //long-> string
      else if (t == typeof(long) || t == typeof(ulong)) {
        retval = Convert.ToString(o);
        modified = true;
      }
      //AdrException is different from others that it has a code that can
      //be assigned to XmlRpcFaultException
      else if(o is AdrException) {
        AdrException e = (AdrException)o;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(string.Format("{0}", e.ToString()));
        retval = new XmlRpcFaultException(e.Code, sb.ToString());
        modified = true;
      } 
      //Still exceptions, XmlRpc.net converts it to XmlRpcFaultException
      else if(o is Exception) {
        Exception e = (Exception)o;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(string.Format("{0}", e.ToString()));
        retval = new Exception(sb.ToString());
        modified = true;
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
     * TODO: there are a centain amount of types compatible in XML-RPC/XmlRpc.Net
     * but never used in Brunet so haven't been implemented. They can be added
     * when AdrConverter knows how to deal with them
     */
    public static object XmlRpc2Adr(object o, out bool modified) {
      object retval;

      if (o == null) {
        retval = o;
        modified = false;
        return retval;
      }

      System.Type t = o.GetType();
      retval = o;
      modified = false;
      if (t == typeof(byte[])) {
        byte[] b = (byte[])o;
        if (b.Length == 0) {
          //empty byte[] array -> null
          retval = null;
          modified = true;
        }
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

      ArrayList ret = new ArrayList();
      foreach (object o in oa) {
        ret.Add(XmlRpc2Adr(o));
      }
      return ret.ToArray();
    }
  }
}
