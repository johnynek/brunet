namespace Brunet {

  /**
   * Holds all the data about a connection
   */
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

    /**
     * same as the above.
     */
    public Connection(Edge e, Address a, ConnectionType ct, StatusMessage sm)
    {
      _e = e;
      _a = a;
      _ct = ConnectionTypeToString(ct);
      _stat = sm;
    }

    protected Address _a;
    public Address Address { get { return _a; } }
    
    protected Edge _e;
    public Edge Edge { get { return _e; } }
    
    protected string _ct;
    public ConnectionType Ct { get { return StringToConnectionType(_ct); } }
    public string ConType { get { return _ct; } }
    
    protected StatusMessage _stat;
    public StatusMessage Status { get { return _stat; } }
    
    /**
     * Return the string for a connection type
     */
    static public string ConnectionTypeToString(ConnectionType t)
    {
      if( t == ConnectionType.StructuredNear ) {
        return "structured.near";
      }
      else if( t == ConnectionType.StructuredShortcut ) {
        return "structured.shortcut";
      }
      else {
        return t.ToString().ToLower();
      }
    }

    /**
     * Return the string representation of a ConnectionType
     */
    static public ConnectionType StringToConnectionType(string s)
    {
      string undotted = s.Replace(".","");
      return (ConnectionType) System.Enum.Parse(typeof(ConnectionType),
                                               undotted,
                                               true);
    }

    /**
     * @return a string representation of the Connection
     */
    public override string ToString()
    {
      return "Edge: " + _e.ToString() + ", Address: " + _a.ToString() + ", ConnectionType: " + _ct;
    }
  }
	  
}
