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
using System.Security.Cryptography;
#if BRUNET_NUNIT
using System.Collections.Specialized;
using NUnit.Framework;
#endif

namespace Brunet {
  public class ISenderFactoryException: Exception  {
    public ISenderFactoryException(string s): base(s) {}
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
  public class ISenderFactory {
    protected ISenderFactory() {}

    protected static char [] _split_char = new char[] {'?', '&'};
    protected static char [] _delim = new char[] {'='};

    /**
     * Returns an instance of an ISender, given its URI representation.
     * @param n node on which the sender is attached. 
     * @param uri URI representation of the sender.
     * @returns an ISender object.
     * @throws ISenderFactoryException when URI is invalid or unsupported. 
     */
    public static ISender CreateInstance(Node n, string uri) {
      if (!uri.StartsWith("sender:")) {
        throw new ISenderFactoryException("Invalid string representation");
      }
      string s = uri.Substring(7);
      string []ss = s.Split(_split_char);
      if (ss[0].Equals("ah")) {        //ah sender
        string []dest = ss[1].Split(_delim);
        Address target = AddressParser.Parse(dest[1]);
        string mode = (ss[2].Split(_delim))[1];
        //Console.WriteLine("{0}, {1}", target, mode);

        if (mode.Equals("greedy")) { //greedy sender
          return new AHGreedySender(n, target);
        } 
        else if (mode.Equals("exact")) { //exact sender
          return new AHExactSender(n, target);
        }
      }
      else if(ss[0].Equals("fw")) {
        //forwarding sender
        string[] relay = ss[1].Split(_delim);
        string[] dest = ss[2].Split(_delim);
        Address forwarder = AddressParser.Parse(relay[1]);
        Address target = AddressParser.Parse(dest[1]);
        short ttl = (short) Int16.Parse((ss[3].Split(_delim))[1]);
        string mode = (ss[4].Split(_delim))[1];
        ushort option = AHPacket.AHOptions.AddClassDefault;
        if (mode.Equals("path")) {
          option = AHPacket.AHOptions.Path;
        }
        else if(mode.Equals("last")) {
          option = AHPacket.AHOptions.Last;
        }
        //Console.WriteLine("{0}, {1}, {2}, {3}", forwarder, target, ttl, option);
        return new ForwardingSender(n, forwarder, target, ttl, option);
      }
      throw new ISenderFactoryException("Unsupported sender.");
    }
  }

#if BRUNET_NUNIT
  [TestFixture]
  public class ISenderFactoryTester {
    [Test]
    public void Test() {
      RandomNumberGenerator rng = new RNGCryptoServiceProvider();      
      AHAddress tmp_add = new AHAddress(rng);
      Node n = new StructuredNode(tmp_add, "unittest");
      object o = ISenderFactory.CreateInstance(n, "sender:ah?dest=brunet:node:JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4&mode=exact");
      Assert.IsTrue(o is AHExactSender);
      o = ISenderFactory.CreateInstance(n, "sender:ah?dest=brunet:node:JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4&mode=greedy");
      Assert.IsTrue(o is AHGreedySender);
      o = ISenderFactory.CreateInstance(n, "sender:fw?relay=brunet:node:JOJZG7VO6RFOEZJ6CJJ2WOIJWTXRVRP4&dest=brunet:node:5FMQW3KKJWOOGVDO6QAQP65AWVZQ4VUQ&ttl=3&mode=path");
      Assert.IsTrue(o is ForwardingSender);      
    }
  }
#endif
}
