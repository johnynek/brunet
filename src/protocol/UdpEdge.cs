/*
 * Dependencies : 
 * 
 * Brunet.Edge;
 * Brunet.Packet;
 * Brunet.TransportAddress;
 */

using System;
using System.Collections;
using System.Net;

namespace Brunet
{

        /**
	 * A Edge which does its transport over the Udp protocol.
	 * The UDP protocol is really better for Brunet.
	 */

  public class UdpEdge : Edge
  {
    /**
     * Adding logger
     */
    /*private static readonly log4net.ILog log =
      log4net.LogManager.GetLogger(System.Reflection.MethodBase.
      GetCurrentMethod().DeclaringType);*/

    protected bool inbound;
    protected bool _is_closed;
    protected Edge.PacketCallback _send_cb;
    
    protected System.Net.EndPoint end;
    public System.Net.EndPoint End {
      get {
        return end;
      }
    }

    protected int _id;
    public int ID { get { return _id; } }

    /**
     * The send_cb is the method which actually does the
     * sending (which is in UdpEdgeListener).
     */
    public UdpEdge(Edge.PacketCallback send_cb,
		   bool is_in,
                   System.Net.IPEndPoint remote_end_point,
		   System.Net.IPEndPoint local_end_point,
		   int id)
    {
      _send_cb = send_cb;
      inbound = is_in;
      end = remote_end_point;
      _is_closed = false;
      _remoteta = new TransportAddress(TAType, (IPEndPoint) end);
      _localta = new TransportAddress(TAType, (IPEndPoint) local_end_point);
      _id = id;
    }

    public override void Close()
    {
      base.Close();
      _is_closed = true;
    }

    public override bool IsClosed
    {
      get
      {
        return (_is_closed);
      }
    }
    public override bool IsInbound
    {
      get
      {
        return inbound;
      }
    }

    protected DateTime _last_out_packet_datetime;
    public override DateTime LastOutPacketDateTime {
      get { return _last_out_packet_datetime; }
    }
   
    public override bool Equals(object o)
    {
      if( o is UdpEdge ) {
        UdpEdge other = (UdpEdge)o;
	return this.ID == other.ID;
      }
      else {
        return false;
      }
    }
    
    public override void Send(Brunet.Packet p)
    {
      _send_cb(p, this);  
      _last_out_packet_datetime = DateTime.Now;
      /**
         * logging of outgoing packets
         */
      //string GeneratedLog = " a new packet was recieved on this edge ";
	string base64String;
        try {
           byte[] packet_buf = new byte[ p.Length ];
	   p.CopyTo(packet_buf, 0);
	   base64String = Convert.ToBase64String(packet_buf);
	}
        catch (System.ArgumentNullException){
        //log.Error("Error: Packet is Null");
               return;
        }
	string GeneratedLog = "OutPacket: " + LocalTA.ToString()+", "+RemoteTA.ToString()+ ", " + base64String;
        //log.Info(GeneratedLog);
	// logging finished

    }

    public override Brunet.TransportAddress.TAType TAType
    {
      get
      {
        return Brunet.TransportAddress.TAType.Udp;
      }
    }

    protected TransportAddress _localta;
    public override Brunet.TransportAddress LocalTA
    {
      get { return _localta; }
    }
    protected TransportAddress _remoteta;
    public override Brunet.TransportAddress RemoteTA
    {
      get { return _remoteta; }
    }
    public void Push(Packet p)
    {
      ReceivedPacketEvent(p);
    }
  }

}
