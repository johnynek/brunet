/*
This program is part of BruNet, a library for the creation of efficient overlay networks.
Copyright (C) 2005  University of California

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

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Brunet {

  /**
   * These are the Major connection types.  Connections can have subtypes,
   * which are denoted with dots followed by these names:
   * e.g. "structured.near" "structed.shortcut" etc...
   */
  public enum ConnectionType
  {
    Leaf,                       //Connections which are point-to-point edge.
    Structured,                 //Connections for routing structured addresses
    Unstructured,               //Connections for routing unstructured addresses
    Unknown                     //Refers to all connections which are not in the above
  }

  /**
   * Holds all the data about a connection
   */
#if BRUNET_NUNIT
  [TestFixture]
#endif
  public class Connection {
#if BRUNET_NUNIT
    //NUnit needs a default constructor
    public Connection() { }
#endif

    /**
     * Prefered constructor for a Connection
     */
    public Connection(Edge e, Address a,
		      string connectiontype,
		      StatusMessage sm, LinkMessage peerlm)
    {
      _e = e;
      _a = a;
      _ct = String.Intern(connectiontype);
      _stat = sm;
      _lm = peerlm;
      _creation_time = DateTime.UtcNow;
      MainType = StringToMainType(_ct);
      _as_dict = new WriteOnce<ListDictionary>();
      _sub_type = new WriteOnce<string>();
    }

    protected DateTime _creation_time;
    public DateTime CreationTime { get { return _creation_time; } }
    protected Address _a;
    public Address Address { get { return _a; } }
    
    protected Edge _e;
    public Edge Edge { get { return _e; } }
    
    protected readonly string _ct;
    public readonly ConnectionType MainType;
    public string ConType { get { return _ct; } }
   
    protected LinkMessage _lm;
    /**
     * Holds the link message that our peer sent us when we made
     * this connection.  This may hold useful information for
     * dealing with NAT or Firewall settings
     */
    public LinkMessage PeerLinkMessage { get { return _lm; } }
    
    protected StatusMessage _stat;
    public StatusMessage Status { get { return _stat; } }
  
    public string SubType {
      get {
        string res = _sub_type.Value;
        if( res == null ) {
          int dot_idx = _ct.IndexOf('.');
          if( dot_idx >= 0 ) {
            res = _ct.Substring(dot_idx);
          }
          else {
            res = String.Empty;
          }
          _sub_type.TrySet(res);
        }
        return res;
      }
    }
   
    protected readonly WriteOnce<ListDictionary> _as_dict;
    protected readonly WriteOnce<string> _sub_type; 
    /**
     * Return the string for a connection type
     */
    static public string ConnectionTypeToString(ConnectionType t)
    {
      if( t == ConnectionType.Structured ) {
        return "structured";
      }
      if( t == ConnectionType.Leaf ) {
        return "leaf";
      }
      return String.Intern( t.ToString().ToLower() );
    }

    /**
     * Doing string operations is not cheap, and we do this a lot
     * so it is worth improving the performance
     */
    static protected Dictionary<string, ConnectionType> _string_to_main_type 
        = new Dictionary<string, ConnectionType>();
    /**
     * Return the string representation of a ConnectionType
     */
    static public ConnectionType StringToMainType(string s)
    {
      ConnectionType result;
      if( false ==_string_to_main_type.TryGetValue(s, out result)) {
        int dot_idx = s.IndexOf('.');
        string maintype = s;
        if( dot_idx > 0 ) {
          maintype = s.Substring(0, dot_idx);
        }
        try {
          result = (ConnectionType)Enum.Parse(typeof(ConnectionType),
                                               maintype,
                                               true);
        }
        catch { result = ConnectionType.Unknown; }
        lock( _string_to_main_type ) {
          _string_to_main_type[String.Intern(s)] = result;
        }
      }
      return result;
    }
 
    /** Return a version of the Dictionary suitable for ADR use
     * maps:
     * "address" => Address.ToString()
     * "sender" => Edge.ToUri()
     * "type" => ConType
     */

    public IDictionary ToDictionary() {
      IDictionary d = _as_dict.Value;
      if( d != null ) {
        return d;
      }
      ListDictionary ld = new ListDictionary();
      ld.Add("address", Address.ToString());
      ld.Add("sender", Edge.ToUri());
      ld.Add("type", ConType);
      _as_dict.TrySet(ld);
      return ld;
    }
    //Keys used in the ToDictionary method
    public static readonly string[] DictKeys = new string[]{"address", "sender", "type"};

    /**
     * @return a string representation of the Connection
     */
    public override string ToString()
    {
      return String.Format("Edge: {0}, Address: {1}, ConnectionType: {2}",
                                    _e, _a, _ct);
    }
#if BRUNET_NUNIT
    [Test]
    public void TestParsing() {
      Assert.AreEqual(ConnectionType.Structured, StringToMainType("structured.near"));
      Assert.AreEqual(ConnectionType.Structured, StringToMainType("structured.shortcut"));
      Assert.AreEqual(ConnectionType.Structured, StringToMainType("structured"));
      Assert.AreEqual(ConnectionType.Unstructured, StringToMainType("unstructured"));
      Assert.AreEqual(ConnectionType.Unknown, StringToMainType("asdasfba"));
    }
#endif
  }
	  
}
