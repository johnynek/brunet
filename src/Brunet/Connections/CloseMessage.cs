/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
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

//using Brunet.ConnectionMessage;
using Brunet;
using System;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Connections
{

  /**
   * The close message is sent and acknowledged
   * when a connection is to be closed
   */
  public class CloseMessage
  {

    public CloseMessage()
    {
      _reason = String.Empty;
    }
    /**
     * Make a close message with a non-empty reason string
     */
    public CloseMessage(string reason)
    {
      _reason = reason;
    }

    protected string _reason;
    public string Reason {
    get { return _reason; }
    }

    override public int GetHashCode() {
      return _reason.GetHashCode();
    }

    override public bool Equals(object o)
    {
      if( o is CloseMessage ) {
        return (((CloseMessage)o).Reason == _reason);
      }
      else {
        return false;
      }
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class CloseMessageTester {
    public CloseMessageTester()  { }

    [Test]
    public void CMTest()
    {
    }
  }
  
#endif

}
