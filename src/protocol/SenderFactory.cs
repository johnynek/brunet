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
using System.Security.Cryptography;
#if BRUNET_NUNIT
using System.Collections.Specialized;
using NUnit.Framework;
#endif

namespace Brunet {

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
   * ForwardingSender: sender:fw?relay=node:[base32 brunet address]&dest=[base32 encoded brunet address]&ttl=ttl&mode=path
   * example - sender:fw?relay=brunet:node:JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4&dest=brunet:node:5FMQW3KKJWOOGVDO6QAQP65AWVZQ4VUQ&ttl=3&mode=path
   */
  public class SenderFactory {
    public static readonly char [] SplitChars = new char[] {'?', '&'};
    public static readonly char [] Delims = new char[] {'='};

    protected static Hashtable _handlers = new Hashtable();

    protected static Hashtable _string_to_ushort = new Hashtable();
    protected static Hashtable _ushort_to_string = new Hashtable();      

    static SenderFactory() {
      _string_to_ushort["greedy"] = AHPacket.AHOptions.Greedy;
      _string_to_ushort["exact"] = AHPacket.AHOptions.Exact;
      _string_to_ushort["path"] = AHPacket.AHOptions.Path;
      _string_to_ushort["last"] = AHPacket.AHOptions.Last;
      _string_to_ushort["default"] = AHPacket.AHOptions.AddClassDefault;
      _string_to_ushort["annealing"] = AHPacket.AHOptions.Annealing;

      _ushort_to_string[AHPacket.AHOptions.Greedy] = "greedy";
      _ushort_to_string[AHPacket.AHOptions.Exact] = "exact";
      _ushort_to_string[AHPacket.AHOptions.Path] = "path";
      _ushort_to_string[AHPacket.AHOptions.Last] = "last";
      _ushort_to_string[AHPacket.AHOptions.AddClassDefault] = "default";
      _ushort_to_string[AHPacket.AHOptions.Annealing] = "annealing";
      }
    
    public static ushort StringToUShort(string mode) {
      if (_string_to_ushort.ContainsKey(mode)) {
        return (ushort) _string_to_ushort[mode];
      }
      throw new SenderFactoryException("Unknown sender mode: " + mode);
    }
    
    public static string UShortToString(ushort mode) {
      if (_ushort_to_string.ContainsKey(mode)) {
        return (string) _ushort_to_string[mode];
      }
      throw new SenderFactoryException("Unknown sender mode: " + mode);
    }


    /** 
     * Register a factory method for parsing sender URIs.
     * @param type type of the sender.
     * @handler factory method for the given type.
     */
    public static void Register(string type, SenderFactoryDelegate handler) {
      _handlers[type] = handler;
    }

    /**
     * Returns an instance of an ISender, given its URI representation.
     * @param n node on which the sender is attached. 
     * @param uri URI representation of the sender.
     * @returns an ISender object.
     * @throws SenderFactoryException when URI is invalid or unsupported. 
     */
    public static ISender CreateInstance(Node n, string uri) {
      if (!uri.StartsWith("sender:")) {
        throw new SenderFactoryException("Invalid string representation");
      }
      string s = uri.Substring(7);
      string []ss = s.Split(SplitChars);
      string type = ss[0];
      if (_handlers.ContainsKey(type)) {
        try {
          SenderFactoryDelegate f = (SenderFactoryDelegate) _handlers[type];
          return f(n, uri);
        } catch {
          new SenderFactoryException("Cannot parse URI for type:" + type);         
        }
      }
      throw new SenderFactoryException("Unsupported sender.");
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
      
      string uri = "sender:ah?dest=brunet:node:JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4&mode=exact";
      ISender s = SenderFactory.CreateInstance(n, uri);
      Assert.IsTrue(s is AHSender);
      Assert.AreEqual(uri, s.ToUri());
      uri = "sender:ah?dest=brunet:node:JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4&mode=greedy";
      s = SenderFactory.CreateInstance(n, uri);
      Assert.IsTrue(s is AHSender);
      Assert.AreEqual(uri, s.ToUri());      
      uri = "sender:fw?relay=brunet:node:JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4&init_mode=greedy&dest=brunet:node:5FMQW3KKJWOOGVDO6QAQP65AWVZQ4VUQ&ttl=3&mode=path";
      s = SenderFactory.CreateInstance(n, uri);
      Assert.IsTrue(s is ForwardingSender);
      Assert.AreEqual(uri, s.ToUri());
    }
  }
#endif
}
