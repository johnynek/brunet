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

#if BRUNET_NUNIT
using System.Security.Cryptography;
using NUnit.Framework;
#endif

namespace Brunet
{
  /**
  <summary>IPHandler is provided to do BrunetRpc natively (as opposed to
  XmlRpc).  This service provides for discovery similar to zeroconf.  This was
  done due to zeroconf not being widespread enough to rely on it as well as too
  many ways to interface with the different implementations.  Discovery runs at
  group address 224.123.123.222:56123.</summary>
  */
  public class IPHandler: ISource
  {
    /// <summary>Runs on 224.123.123.222:56123</summary>
    protected readonly Socket _mc;
    /// <summary>The unicast socket, the port is randomly selected.</summary>
    protected readonly Socket _uc;
    /// <summary>Multicast address.</summary>
    public static readonly IPAddress mc_addr = IPAddress.Parse("224.123.123.222");
    /// <summary>Multicast port.</summary>
    public static readonly int mc_port = 56123;
    /// <summary>Multicast end point.</summary>
    public static readonly EndPoint mc_endpoint = new IPEndPoint(mc_addr, mc_port);
    /// <summary>The system is running and so should _listen_thread.</summary>
    protected volatile bool _running;
    /**  <summary>Thread dedicated to reading from the unicast and multicast
    sockets</summary>*/
    protected readonly Thread _listen_thread;
    /// <summary>All messages must be preended with the magic COOKIE!
    public static readonly MemBlock MagicCookie =
        MemBlock.Reference(new byte[] {0x50, 0x87, 0xbd, 0x29});

    protected class Sub {
      public readonly IDataHandler Handler;
      public readonly object State;
      public Sub(IDataHandler h, object s) { Handler = h; State =s; }
      public void Handle(MemBlock b, ISender f) { Handler.HandleData(b, f, State); }
    }
    protected readonly object _sync;
    protected volatile Sub _sub;

    /**
    <summary>Creates a new IPHandler object by initializing the multicast and
    unicast socket.</summary>
    <remarks>The multicast socket is optional as nodes can discover other
    working nodes using only the unicast socket.  The steps for setup for a
    multicast socket are bind to 0.0.0.0:port, add membership to all
    IP Addresses on the node, allow MulticastLoopback.  The steps for the
    unicast socket are to create socket andbind to 0.0.0.0 and random port.
    Afterwhich the _listen_thread is started in the background.</remarks>
    */
    public IPHandler() {
      try {
        _mc = new Socket(AddressFamily.InterNetwork, SocketType.Dgram,
                        ProtocolType.Udp);
        // Allows for multiple Multicast clients on the same host!
        _mc.SetSocketOption(SocketOptionLevel.Socket, 
                            SocketOptionName.ReuseAddress, true);
        _mc.Bind(new IPEndPoint(IPAddress.Any, mc_port));

        // We need to add a group membership for all IPAddresses on the local machine
        foreach(IPAddress ip in GetLocalIPAddresses()) {
          /* the LAN address tends to throw exceptions in Vista 64, while
          loopback doesn't... doing this, we make sure to at least get loopback
          discovery. */
          try{
            _mc.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
              new MulticastOption(mc_addr, ip));
          }
          catch {}
        }
        _mc.MulticastLoopback = true;
      }
      catch {
        _mc = null;
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, "Unable to start listening on the" +
            "multicast port, but we can still send and request services.");
      }

      _uc = new Socket(AddressFamily.InterNetwork, SocketType.Dgram,
                       ProtocolType.Udp);
      // It won't send to other mono applications on the same host otherwise...
      _uc.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
      _uc.Bind(new IPEndPoint(IPAddress.Any, 0));
      _running = true;
      _sync = new Object();
      _listen_thread = new Thread(Listen);
      _listen_thread.IsBackground = true;
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

    /// <summary>Called to interrupt the _listen_thread.</summary>
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

    /**
    <summary>The _listen_threads method, reads from sockets and let's the node
    handle the incoming data.</summary>
    */
    protected void Listen() {
      if (Thread.CurrentThread.Name == null) {
        Thread.CurrentThread.Name = "iphandler_thread";
      }
      ArrayList sockets = new ArrayList();
      sockets.Add(_uc);
      if(_mc != null) {
        sockets.Add(_mc);
      }

      byte[] buffer =  new byte[Packet.MaxLength];
      DateTime last_debug = DateTime.UtcNow;
      TimeSpan debug_period = TimeSpan.FromSeconds(5);
      while(_running) {
        if (ProtocolLog.Monitor.Enabled) {
          DateTime now = DateTime.UtcNow;
          if (now - last_debug > debug_period) {
            last_debug = now;
            ProtocolLog.Write(ProtocolLog.Monitor, String.Format("I am alive: {0}", now));
          }
        }
        try {
          ArrayList readers = (ArrayList) sockets.Clone();
          Socket.Select(readers, null, null, 10000000); //10 seconds
          foreach(Socket socket in readers) {
            EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
            int rec_bytes = socket.ReceiveFrom(buffer, ref ep);
            Sub s = _sub;
            //s can't change once we've read it.
            if( s != null) {
              MemBlock packet = MemBlock.Copy(buffer, 0, rec_bytes);
//              MemBlock cookie = packet.Slice(0, 4);
//              if(cookie.Equals(MagicCookie)) {
//                packet = packet.Slice(4);
              ISender sender = CreateUnicastSender(ep);
              s.Handle(packet, sender);
//              }
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
          if(!_running) {
            ProtocolLog.WriteIf(ProtocolLog.Exceptions, x.ToString());
          }
        }
      }
    }

    /**
    <summary>Creates a new UnicastSender using the IPHandlers unicast socket.
    </summary>
    <param name="ep">The remote network end point where the UnicastSender
    should send the data.</param>
    <returns>UnicastSender</returns>
    */
    public ISender CreateUnicastSender(EndPoint ep) {
      return new UnicastSender(_uc, ep);
    }

    /**
    <summary>Creates a new MulticastSender using the IPHandlers multicastcast
    socket.  This sends to all network devices on the computer.</summary>
    <returns>MulticastSender</returns>
    */
    public ISender CreateMulticastSender() {
      return new MulticastSender(_uc);
    }

    /**
    <summary>Creates a Multicast Sender using the unicast socket, so that
    other nodes know where they can reach us.  Only sends to the addresses
    in the array.</summary>
    <param name="LocalIPAddresses">The local ip addresses we should send on.</param>
    <returns>MulticastSender</returns>
    */
    public ISender CreateMulticastSender(IPAddress[] LocalIPAddresses) {
      return new MulticastSender(_uc, LocalIPAddresses);
    }

    /**
    <summary>IPAddress.Address is obsolete, we use our own method to convert
    to an int in case the method is removed.</summary>
    <param name="addr">The address to convert to an integer.</param>
    <exception cref="System.Exception">When not an IPv4 address.</exception>
    */
    public static int IPAddressToInt(IPAddress addr) {
      byte[] addr_bytes = addr.GetAddressBytes();
      if(addr_bytes.Length != 4) {
        throw new Exception("This is only supported by IPv4!");
      }
      int val = 0;
      for(int i = 3; i >= 0; i--) {
        val = (val << 8) | addr_bytes[i];
      }
      return val;
    }

    /**
    <summary>Returns an array of all the IPAddresses of the local machine.
    Manually add loopback sense Windows does not have a loopback interface.
    </summary>
    <returns>An array of IPAddresses of the local machine.</returns>
     */
    public static IPAddress[] GetLocalIPAddresses() {
      IPAddress[] base_addresses = null;
      try {
        base_addresses = Dns.GetHostAddresses(Dns.GetHostName());
      }
      catch {
        base_addresses = new IPAddress[0];
      }

      IPAddress[] addresses = new IPAddress[base_addresses.Length + 1];
      base_addresses.CopyTo(addresses, 0);
      addresses[addresses.Length - 1] = IPAddress.Loopback;
      return addresses;
    }
  }

  /**  <summary>Provides a method of sending data over IPHandler's unicast
  socket.</summary>*/
  public class UnicastSender: ISender
  {
    /// <summary>The remote end point to send to.</summary>
    public readonly EndPoint EndPoint;
    /// <summary>The socket to send the data on.</summary>
    protected readonly Socket _s;

    /**
    <summary>Sends the data over the unicast socket.</summary>
    <param name="data">The data to send.</summary>
    */
    public virtual void Send(ICopyable data) {
      // Silly users can trigger a handful of exceptions here...
      try {
//        data = new CopyList(IPHandler.MagicCookie, data);
        byte[] buffer = new byte[data.Length];
        int length = data.CopyTo(buffer, 0);

        _s.SendTo(buffer, 0, length, 0, EndPoint);
      }
      // Can't pass the fact that the IPHandler is not running :-/
      catch (ObjectDisposedException) {}
      catch (Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, "ERROR: " + e);
      }
    }

    /**
    <summary>Creates a new unicast sender that will send over the provided
    socket to the specified endpoint.</summary>
    <param name="s">The socket to use in sending.</param>
    <param name="ep">The remote end point to send to.</param>
    </summary>
    */
    public UnicastSender(Socket s, EndPoint ep) {
      _s = s;
      EndPoint = ep;
    }

    /**
    <summary>ISender objects need to have semantically meaningful Equals.
    </summary>
    <param name="o">The object to compare to.</param>
    <returns>True if they share a common end point.</returns>
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
        return (_s.Equals( other._s ) && EndPoint.Equals( other.EndPoint ));
      }
    }

    /**
    <summary>Uses the EndPoints HashCode.</summary>
    <returns>The HashCode of the EndPoint.</returns>
    */
    public override int GetHashCode() {
      return EndPoint.GetHashCode();
    }
  }

  public class MulticastSender: UnicastSender 
  {
    /// <summary>If defined, the addresses to send on.</summary>
    public readonly IPAddress[] LocalIPAddresses;
    /**
    <summary>Creates a new multicast sender that will send over the provided
    socket.</summary>
    <param name="s">The socket to use in sending.</param>
    </summary>
    */
    public MulticastSender(Socket s):base(s, IPHandler.mc_endpoint) {
      this.LocalIPAddresses = null;
    }

    /**
    <summary>Creates a new multicast sender that will send over the provided
    socket using the specified addresses.</summary>
    <param name="s">The socket to use in sending.</param>
    <param name="LocalIPAddresses">The IP Address of the interfaces to send
    over.</param>
    </summary>
    */
    public MulticastSender(Socket s, IPAddress[] LocalIPAddresses): base(s, IPHandler.mc_endpoint) {
      this.LocalIPAddresses = LocalIPAddresses;
    }

    /**
    <summary>Sends the data over the multicast socket.</summary>
    <param name="data">The data to send.</summary>
    */
    public override void Send(ICopyable data) {
      IPAddress[] ips = LocalIPAddresses;
      if(ips == null) {
        ips = IPHandler.GetLocalIPAddresses();
      }
      // Silly users can trigger a handful of exceptions here...
      try {
//        data = new CopyList(IPHandler.MagicCookie, data);
        byte[] buffer = new byte[data.Length];
        int length = data.CopyTo(buffer, 0);
        // I REALLY HATE THIS but we can't be setting this option in more than one thread!
        lock(_s) {
          foreach(IPAddress ip in ips) {
            /*
             * This can throw an exception on an invalid address, we need to skip it and move on!
             * Never showed to be an issue in Linux, but Windows does some weird things.
             */
            try {
              _s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface,
                                IPHandler.IPAddressToInt(ip));
            }
            catch {
              continue;
            }
              _s.SendTo(buffer, 0, length, 0, EndPoint);
          }
        }
      }
      // Can't pass the fact that the IPHandler is not running :-/
      catch (ObjectDisposedException) {}
      catch (Exception e) {
        ProtocolLog.WriteIf(ProtocolLog.Exceptions, "ERROR: " + e);
      }
    }
  }
#if BRUNET_NUNIT
  [TestFixture]
  public class IPHandlerTest{
    [Test]
    public void Test() {
      IPAddress[] base_addresses = Dns.GetHostAddresses(Dns.GetHostName());
      ArrayList local_ips = new ArrayList(base_addresses);
      local_ips.Add(IPAddress.Loopback);
      ArrayList ips = new ArrayList(IPHandler.GetLocalIPAddresses());
      foreach(IPAddress addr in local_ips) {
        Assert.IsTrue(ips.Contains(addr), addr + " is not in ips");
      }
      Assert.AreEqual(ips.Count, local_ips.Count, "Count");
    }
  }
#endif
}
