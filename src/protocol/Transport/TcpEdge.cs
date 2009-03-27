/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005 University of California
Copyright (C) 2008 P. Oscar Boykin <boykin@pobox.com>, University of Florida

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
using System.Threading;
using System.Collections;
using System.Net;
using System.Net.Sockets;

namespace Brunet
{

  /**
  * A Edge which does its transport over the TCP protocol.
  */

  public class TcpEdge : Edge
  {
    public readonly Socket Socket;
    protected readonly byte[] _buffer;
    public static readonly int MAX_PACKET = Int16.MaxValue;
    protected int _written;
    protected int _flush_ptr;
    /**
     * The send_cb is the method which actually does the
     * sending (which is in UdpEdgeListener).
     */
    public TcpEdge(IEdgeSendHandler send_cb,
                   bool is_in, Socket s) : base(send_cb, is_in)
    {
      //This will update both the end point and the remote TA
      Socket = s;
      _localta = TransportAddressFactory.CreateInstance(TAType, (IPEndPoint) s.LocalEndPoint);
      _remoteta = TransportAddressFactory.CreateInstance(TAType, (IPEndPoint) s.RemoteEndPoint);
      _buffer = new byte[ MAX_PACKET + 2 ]; //+2 to include the size of the packet.
      _written = 0;
      _flush_ptr = 0;
    }

    public override Brunet.TransportAddress.TAType TAType { get { return Brunet.TransportAddress.TAType.Tcp; } }

    protected readonly TransportAddress _localta;
    public override Brunet.TransportAddress LocalTA { get { return _localta; } }
    /**
     * If we are InBound, we are not Ephemeral 
     */
    public override bool LocalTANotEphemeral { get { return IsInbound; } }
    
    protected readonly TransportAddress _remoteta;
    public override TransportAddress RemoteTA { get { return _remoteta; } }
    
    /**
     * If we are InBound, the other is outbound, and thus, ephemeral
     */
    public override bool RemoteTANotEphemeral { get { return (false == IsInbound); } }
    // /////////
    // Methods
    // ///////

    /**
     * Try to write as much of the buffer as possible into the socket
     * @return true if we successfully flushed, otherwise there is more to
     * write
     */
    public bool Flush() {
      int to_send = _written - _flush_ptr;
      if( to_send != 0 ) {
            /*
             * According to the documentation:
             * http://msdn.microsoft.com/en-us/library/ms145162.aspx
             * "In nonblocking mode, Send may complete successfully even if it
             * sends less than the number of bytes you request. It is your
             * application's responsibility to keep track of the number of
             * bytes sent and to retry the operation until the application
             * sends the requested number of bytes."
             */
        int sent = 0;
        try {
          sent = Socket.Send(_buffer, _flush_ptr, to_send, SocketFlags.None);
        }
        catch(SocketException sx) {
          SocketError serr = sx.SocketErrorCode;
          if( serr == SocketError.WouldBlock ) {
            //We couldn't flush now, so return false:
            return false;
          }
          else {
            //all other cases don't seem to be transient
            throw new EdgeException(false, String.Format("TCP SocketError: {0} on: {1}", serr, this), sx);
          }
        }
        catch(ObjectDisposedException odx) {
          throw new EdgeException(false, String.Format("Socket disposed while sending on: {0}", this), odx);
        }
        if( to_send == sent ) {
          //Reset:
          _written = 0;
          _flush_ptr = 0;
          return true;
        }
        else {
          _flush_ptr += sent;
          return false;
        }
      }
      else {
        return true;
      }
    }
    /**
     * The caller must make sure that this is synchronized, it is not
     * a thread-safe method
     * This method either writes the whole packet into the buffer space,
     * or it throws an exception.  There is no other case.
     */
    public void WriteToBuffer(ICopyable p) {
      int plength = p.CopyTo(_buffer, _written + 2);
      if( plength > Int16.MaxValue ) {
        throw new EdgeException(true,
                    String.Format("Packet too long: {0}",plength));
      }
      // 
      //The length at the beginning:
      NumberSerializer.WriteShort((short)plength, _buffer, _written);
      //Also write the length at the end so we can detect a bad packet
      //as we have seen on planetlab:
      NumberSerializer.WriteShort((short)plength, _buffer, _written + 2 + plength);
      //If the buffer did not fill up, we have not yet thrown an exception:
      _written += (plength + 4);
    }

  }

}
