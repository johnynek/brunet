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

namespace Brunet {

  /**
   * Holds all the data about a connection
   */
#if BRUNET_NUNIT
  [TestFixture]
#endif
  public class Connection {

    /**
     * Prefered constructor for a Connection
     */
    public Connection(Edge e, Address a, string connectiontype, StatusMessage sm)
    {
      _e = e;
      _a = a;
      _ct = connectiontype;
      _stat = sm;
    }

    protected Address _a;
    public Address Address { get { return _a; } }
    
    protected Edge _e;
    public Edge Edge { get { return _e; } }
    
    protected string _ct;
    public ConnectionType MainType { get { return StringToMainType(_ct); } }
    public string ConType { get { return _ct; } }
    
    protected StatusMessage _stat;
    public StatusMessage Status { get { return _stat; } }
    
    /**
     * Return the string for a connection type
     */
    static public string ConnectionTypeToString(ConnectionType t)
    {
      return t.ToString().ToLower();
    }

    /**
     * Return the string representation of a ConnectionType
     */
    static public ConnectionType StringToMainType(string s)
    {
      int dot_idx = s.IndexOf('.');
      string maintype = s;
      if( dot_idx > 0 ) {
        maintype = s.Substring(0, dot_idx);
      }
      try {
        return (ConnectionType) System.Enum.Parse(typeof(ConnectionType),
                                               maintype,
                                               true);
      }
      catch(System.Exception x) {
      }
      return ConnectionType.Unknown;
    }

    /**
     * @return a string representation of the Connection
     */
    public override string ToString()
    {
      return "Edge: " + _e.ToString() + ", Address: " + _a.ToString() + ", ConnectionType: " + _ct
	      + ", Maintype: " + StringToMainType(_ct);
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
