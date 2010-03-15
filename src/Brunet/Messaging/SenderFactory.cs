/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007 P. Oscar Boykin <boykin@pobox.com>, Arijit Ganguly <aganguly@acis.ufl.edu> University of Florida

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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
#if BRUNET_NUNIT
using System.Collections.Specialized;
using NUnit.Framework;
#endif

using Brunet.Util;

namespace Brunet.Messaging {

  public delegate ISender SenderFactoryDelegate(Node n, string uri);

  public class SenderFactoryException: Exception  {
    public SenderFactoryException(string s): base(s) {}
  }
  
  /**
   * Factory class for creating ISender objects from their
   * URI encodings. 
   * AHExactSender - sender:ah?dest=node:[base32 brunet address]&mode=exact
   * example - sender:ah?dest=brunet:node:JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4&mode=exact
   *
   * AHGreedySender - sender:ah?dest=node:[base32 brunet address]&mode=greedy
   * example - sender:ah?dest=brunet:node:JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4&mode=exact
   *
   * ForwardingSender: sender:fw?relay=node:[base32 brunet address]&init_mode=[initial routing mode]&dest=[base32 encoded brunet address]&ttl=ttl&mode=path
   * example - sender:fw?relay=brunet:node:JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4&init_mode=greedy&dest=brunet:node:5FMQW3KKJWOOGVDO6QAQP65AWVZQ4VUQ&ttl=3&mode=path
   */
  public class SenderFactory {
    public static readonly char [] SplitChars = new char[] {'?', '&'};
    public static readonly char [] Delims = new char[] {'='};

    protected readonly static Dictionary<string, SenderFactoryDelegate> _handlers = new Dictionary<string, SenderFactoryDelegate>();

    /** 
     * Register a factory method for parsing sender URIs.
     * @param type type of the sender.
     * @handler factory method for the given type.
     */
    public static void Register(string type, SenderFactoryDelegate handler) {
      lock( _handlers ) {
        _handlers[type] = handler;
      }
    }

    /**
     * Returns an instance of an ISender, given its URI representation.
     * @param n node on which the sender is attached. 
     * @param uri URI representation of the sender.
     * @returns an ISender object.
     * @throws SenderFactoryException when URI is invalid or unsupported. 
     */
    public static ISender CreateInstance(Node n, string uri) {
      int varidx;
      try {
        string type = GetScheme(uri, out varidx);
        return _handlers[type](n, uri);
      } catch {
        throw new SenderFactoryException("Cannot parse URI: " + uri);         
      }
    }

    /** create a URI string sender:scheme?k1=v1&k2=v2
     * @param scheme the name for this sender
     * @param opts the key-value pairs to encode
     * @return uri
     */
    public static string EncodeUri(string scheme, IDictionary<string, string> opts) {
      List<string> keys = new List<string>(opts.Keys);
      keys.Sort();
      StringWriter sw = new StringWriter();
      sw.Write("sender:{0}", scheme);
      string pattern = "?{0}={1}";
      foreach(string key in keys) {
        sw.Write(pattern, System.Web.HttpUtility.UrlEncode(key), System.Web.HttpUtility.UrlEncode(opts[key])); 
        //For the next time, use a different pattern:
        pattern = "&{0}={1}";
      }
      return sw.ToString();
    }
    /** Decode a URI into a scheme and key-value pairs
     * @param uri the URI to decode
     * @param scheme the for this URI
     * @return key-value pairs encoded
     */
    public static IDictionary<string, string> DecodeUri(string uri, out string scheme) {
      int varidx;
      scheme = GetScheme(uri, out varidx);
      if( varidx > 0 ) {
        string vars = uri.Substring(varidx);
        string[] kvpairs = vars.Split(SplitChars);
        Dictionary<string, string> result = new Dictionary<string, string>(kvpairs.Length);
        foreach(string kvpair in kvpairs) {
          int eq_idx = kvpair.IndexOf('=');
          string key = kvpair.Substring(0, eq_idx);
          string val = kvpair.Substring(eq_idx + 1);
          result.Add(System.Web.HttpUtility.UrlDecode(key), System.Web.HttpUtility.UrlDecode(val));
        }
        return result;
      }
      else {
        return new Dictionary<string,string>();
      }
    }

    public static string GetScheme(string uri, out int varidx) {
      if (!uri.StartsWith("sender:")) {
        throw new SenderFactoryException("Invalid string representation");
      }
      int idx = uri.IndexOf('?');
      if( idx > 0 ) {
        varidx = idx + 1;
        return uri.Substring(7, idx - 7);
      }
      else {
        varidx = -1;
        return uri.Substring(7);
      }
    }
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class SenderFactoryTester {
    [Test]
    public void Test() {
      RandomNumberGenerator rng = new RNGCryptoServiceProvider();      
      AHAddress tmp_add = new AHAddress(rng);
      Node n = new StructuredNode(tmp_add, "unittest");
      AHSender ah = new AHSender(n, AddressParser.Parse("brunet:node:JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4"));
      ForwardingSender fs = new ForwardingSender(n, 
                                                 AddressParser.Parse("brunet:node:JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4"),
                                                 AddressParser.Parse("brunet:node:5FMQW3KKJWOOGVDO6QAQP65AWVZQ4VUQ"));
      
      string uri = "sender:ah?dest=JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4&mode=exact";
      ISender s = SenderFactory.CreateInstance(n, uri);
      Assert.IsTrue(s is AHSender);
      Assert.AreEqual(uri, s.ToUri());
      uri = "sender:ah?dest=JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4&mode=greedy";
      
      //Create the above programatically
      IDictionary<string, string> param_args = new Dictionary<string,string>();
      param_args["dest"] = "JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4";
      param_args["mode"] = "greedy";
      string uri0 = SenderFactory.EncodeUri("ah", param_args); 
      Assert.AreEqual(uri, uri0, "EncodeUri works");
      //Check decode:
      string scheme;
      param_args = SenderFactory.DecodeUri(uri, out scheme);
      Assert.AreEqual(scheme, "ah", "Scheme decoded");
      Assert.AreEqual(param_args.Count, 2, "2 parameters in uri");
      Assert.AreEqual(param_args["dest"], "JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4", "Extracted address");
      Assert.AreEqual(param_args["mode"], "greedy", "got mode");

      s = SenderFactory.CreateInstance(n, uri);
      Assert.IsTrue(s is AHSender);
      Assert.AreEqual(uri, s.ToUri());      
      string furi = "sender:fw?relay=JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4&init_mode=greedy&dest=5FMQW3KKJWOOGVDO6QAQP65AWVZQ4VUQ&ttl=3&mode=path";
      s = SenderFactory.CreateInstance(n, furi);
      Assert.IsTrue(s is ForwardingSender);
      Assert.AreEqual(furi, s.ToUri());
    }
  }
#endif
}
