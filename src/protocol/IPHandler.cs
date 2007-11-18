/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida
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
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Brunet
{
  /**
   * We use Brunet IPHandler due to there not being a single zeroconf 
   * service for all OS's, this makes use of multicast, specifically 
   * destination 224.123.123.222:56018.  Use a random UDP port for unicast 
   * communication.
   */

  public class IPHandler
  {
    protected static Socket _mc, _uc;
    public Socket UnicastSocket { get { return _uc; } }
    public static readonly IPAddress mc_addr = IPAddress.Parse("224.123.123.222");
    public static readonly int mc_port = 56123;
    public static readonly EndPoint mc_endpoint = new IPEndPoint(mc_addr, mc_port);
    protected bool _running;
    protected Node _node;

    public IPHandler(Node node) {
      _node = node;
      try {
        _mc = new Socket(AddressFamily.InterNetwork, SocketType.Dgram,
                        ProtocolType.Udp);
        // Allows for multiple Multicast clients on the same host!
        _mc.SetSocketOption(SocketOptionLevel.Socket, 
                                SocketOptionName.ReuseAddress, true);
        _mc.Bind(mc_endpoint);
        _mc.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
          new MulticastOption(mc_addr, IPAddress.Any));
      }
      catch {
        _mc = null;
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, "Unable to start listening on the" +
            "multicast port, but we can still send and request services.");
      }

      _uc = new Socket(AddressFamily.InterNetwork, SocketType.Dgram,
                       ProtocolType.Udp);
      _uc.Bind(new IPEndPoint(IPAddress.Any, 0));

      _running = true;
      (new Thread(Listen)).Start();
    }

    public void Stop() {
      _running = false;
      _uc.Close();
      if(_mc != null) {
        _mc.Close();
      }
    }

    protected void Listen() {
      ArrayList sockets = new ArrayList();
      sockets.Add(_uc);
      if(_mc != null) {
        sockets.Add(_mc);
      }

      byte[] buffer =  new byte[Packet.MaxLength];

      while(_running) {
        try {
          ArrayList readers = (ArrayList) sockets.Clone();
          Socket.Select(readers, null, readers, -1);
          foreach(Socket socket in readers) {
            EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
            int rec_bytes = socket.ReceiveFrom(buffer, ref ep);

            MemBlock packet = MemBlock.Copy(buffer, 0, rec_bytes);
            UnicastSender us = new UnicastSender(_node, ep);
            _node.Announce(packet, us);
          }
        }
        catch(ObjectDisposedException odx) {
          //If we are no longer running, this is to be expected.
          if(_running) {
          //If we are running print it out
            ProtocolLog.WriteIf(ProtocolLog.Exceptions, odx.ToString());
          }
          break;
        }
        catch(Exception x) {
          ProtocolLog.WriteIf(ProtocolLog.Exceptions, x.ToString());
        }
      }
    }

    /**
     * Send an ICopyable to the the specified end point, this should only
     * be called by IPSender classes.
     * @param ep the end point to send to
     * @param data an ICopyable of data to send
     */
    public void Send(EndPoint ep, ICopyable data)
    {
      // Silly users can trigger a handful of exceptions here...
      try {
        byte[] buffer = new byte[data.Length];
        int length = data.CopyTo(buffer, 0);

        _uc.SendTo(buffer, 0, length, 0, ep);
      }
      catch (Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, "ERROR: " + e);
      }
    }
  }

  public abstract class IPSender: ISender
  {
    protected EndPoint _ep;
    protected Node _node;

    public virtual void Send(ICopyable data)
    {
      _node.IPHandler.Send(_ep, data);
    }
  }

  public class UnicastSender: IPSender
  {
    public UnicastSender(Node node, EndPoint ep)
    {
      _node = node;
      _ep = ep;
    }
  }

  public class MulticastSender: IPSender
  {
    public MulticastSender(Node node)
    {
      _node = node;
      _ep = IPHandler.mc_endpoint;
    }
  }
}
