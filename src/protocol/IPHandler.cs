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

  public class IPHandler: ISource
  {
    protected readonly Socket _mc, _uc;
    public static readonly IPAddress mc_addr = IPAddress.Parse("224.123.123.222");
    public static readonly int mc_port = 56123;
    public static readonly EndPoint mc_endpoint = new IPEndPoint(mc_addr, mc_port);
    protected volatile bool _running;
    protected readonly Thread _listen_thread;

    protected class Sub {
      public readonly IDataHandler Handler;
      public readonly object State;
      public Sub(IDataHandler h, object s) { Handler = h; State =s; }
      public void Handle(MemBlock b, ISender f) { Handler.HandleData(b, f, State); }
    }
    protected readonly object _sync;
    protected volatile Sub _sub;

    public IPHandler() {
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
      _sync = new Object();
      _listen_thread = new Thread(Listen);
      _listen_thread.Start();
    }

    public virtual void Subscribe(IDataHandler hand, object state) {
      lock( _sync ) {
        _sub = new Sub(hand, state);
      }
    }
    public virtual void Unsubscribe(IDataHandler hand) {
      lock( _sync ) {
        if( _sub.Handler == hand ) {
          _sub = null;
        }
        else {
          throw new Exception(String.Format("Handler: {0}, not subscribed", hand));
        }
      }
    }

    public void Stop() {
      _running = false;
      lock( _sync ) {
        _sub = null;
      }
      _uc.Close();
      if(_mc != null) {
        _mc.Close();
      }
      if( Thread.CurrentThread != _listen_thread ) {
        // Join if we are in a different thread
        _listen_thread.Join();
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
          Socket.Select(readers, null, readers, 10000000); //10 seconds
          foreach(Socket socket in readers) {
            EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
            int rec_bytes = socket.ReceiveFrom(buffer, ref ep);
            Sub s = _sub;
            //s can't change once we've read it.
            if( s != null) {
              MemBlock packet = MemBlock.Copy(buffer, 0, rec_bytes);
              ISender sender = CreateUnicastSender(ep);
              s.Handle(packet, sender);
            }
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

    public ISender CreateUnicastSender(EndPoint ep) {
      return new UnicastSender(_uc, ep);
    }

    public ISender CreateMulticastSender() {
      return new MulticastSender(_uc);
    }

    /**
     * IPAddress.Address is obsolete, we use our own method to convert to an
     * int in case the method is removed
     * @param addr the address to convert to an integer
     * @throws Exception when not an IPv4 address
     */
    public static int IPAddressToInt(IPAddress addr) {
      byte[] addr_bytes = addr.GetAddressBytes();
      if(addr_bytes.Length != 4) {
        throw new Exception("This is only supported by IPv4!");
      }
      return NumberSerializer.ReadInt(addr_bytes, 0);
    }

    /**
     * Returns an array of all the IPAddresses of the local machine
     */
    public static IPAddress[] GetLocalIPAddresses() {
      return Dns.GetHostAddresses(Dns.GetHostName());
    }
  }

  public class UnicastSender: ISender
  {
    protected readonly EndPoint _ep;
    protected readonly Socket _s;

    public virtual void Send(ICopyable data)
    {
      // Silly users can trigger a handful of exceptions here...
      try {
        byte[] buffer = new byte[data.Length];
        int length = data.CopyTo(buffer, 0);

        _s.SendTo(buffer, 0, length, 0, _ep);
      }
      catch (Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, "ERROR: " + e);
      }
    }

    public UnicastSender(Socket s, EndPoint ep)
    {
      _s = s;
      _ep = ep;
    }

    /**
     * ISender objects need to have semantically meaningful Equals
     */
    public override bool Equals(object o) {
      UnicastSender other = o as UnicastSender;
      if( other == null ) {
        return false;
      }
      else if( Object.ReferenceEquals(this, o) ) {
        return true;
      }
      else {
        return (_s.Equals( other._s ) && _ep.Equals( other._ep ));
      }
    }

    public override int GetHashCode() {
      return _ep.GetHashCode();
    }
  }

  public class MulticastSender: UnicastSender
  {
    public MulticastSender(Socket s):base(s, IPHandler.mc_endpoint){}

    public override void Send(ICopyable data) {
      IPAddress[] ips = IPHandler.GetLocalIPAddresses();
      // Silly users can trigger a handful of exceptions here...
      try {
        byte[] buffer = new byte[data.Length];
        int length = data.CopyTo(buffer, 0);
        // I REALLY HATE THIS but we can't be setting this option in more than one thread!
        lock(_s) {
          foreach(IPAddress ip in ips) {
            _s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface,
                              IPHandler.IPAddressToInt(ip));
            _s.SendTo(buffer, 0, length, 0, _ep);
          }
        }
      }
      catch (Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, "ERROR: " + e);
      }
    }
  }
}
