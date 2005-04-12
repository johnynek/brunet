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

/**
 * Dependencies
 * Brunet.BrunetEventDescriptor 
 * Brunet.AHAddress
 * Brunet.BigInteger
 */
using System;
using System.Collections;
using System.IO;
using System.Globalization;
using System.Xml;
using System.Text;
using System.Xml.Serialization;
using System.Net;
using System.Net.Sockets;

namespace Brunet{

public class BrunetLogger
{

  private String _connection_log_file;
  private AHAddress _local_ahaddress;
  private bool _net_stream = false;
  private String _icmp_ping_log_file;
  private String _brunet_ping_log_file;
  private String _packet_log_file;
  /// _sw is the network tcp stream directly streaming data back to Cantor
  /// fs is the stream for the connection logs
  /// bp_sw is the stream for brunet-ping logs
  /// icmp_sw is the stream for the icmp-ping logs
  /// packet_sw is the stream for logging packet info (size, type, etc)
  private StreamWriter _sw;  /// fs, bp_sw, icmp_sw, packet_sw;
  private Socket sock;
  private IPEndPoint ipep;
  private static DateTime start_time;
    
  [XmlAttribute("LogFile")] 
  public string LogFile
  {
    get{
      return _connection_log_file;
    }
    set
    {
      _connection_log_file = value;
    }
  }	 

  [XmlAttribute("LocalAHAddress")] 
  public AHAddress LocalAHAddress
  {
    get{
      return _local_ahaddress;
    }
    set
    {
      _local_ahaddress = value;
    }
  }	

  public BrunetLogger(){
    start_time = DateTime.Now;
    String _dir = "./data/";
    int port = 25000;
    _connection_log_file = _dir + "brunetadd" + Convert.ToString(port) + ".log";
    _icmp_ping_log_file = _dir + "icmp-ping" + Convert.ToString(port) + ".log";
    _brunet_ping_log_file = _dir + "brunet-ping" + Convert.ToString(port) + ".log";
    _packet_log_file = _dir + "packet" + Convert.ToString(port) + ".log";
  }
  
  public BrunetLogger(int port, AHAddress local_add)
  {
    String _dir = "./data/";
    _connection_log_file = _dir + "brunetadd" + Convert.ToString(port) + ".log";
    _icmp_ping_log_file = _dir + "icmp-ping" + Convert.ToString(port) + ".log";
    _brunet_ping_log_file = _dir + "brunet-ping" + Convert.ToString(port) + ".log";
    _packet_log_file = _dir + "packet" + Convert.ToString(port) + ".log";
    _local_ahaddress = local_add;
    
    start_time = DateTime.Now;
  }

  public BrunetLogger(int port, AHAddress local_add, bool net_stream, String server_ipadd, int server_port)
  {
    String _dir = "./data/";
    _connection_log_file = _dir + "brunetadd" + Convert.ToString(port) + ".log";
    _icmp_ping_log_file = _dir + "icmp-ping" + Convert.ToString(port) + ".log";
    _brunet_ping_log_file = _dir + "brunet-ping" + Convert.ToString(port) + ".log";
    _packet_log_file = _dir + "packet" + Convert.ToString(port) + ".log";
    _local_ahaddress = local_add;
    _net_stream = net_stream;

    if(_net_stream){ 
       ipep = new IPEndPoint(IPAddress.Parse(server_ipadd), server_port);
       sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
       try{
          sock.Connect(ipep);
       }
       catch(SocketException e){
          Console.WriteLine("Unable to connect to server.");
	  Console.WriteLine(e.ToString());
	  Environment.Exit(0);
       }
       /*Uri uri = new Uri(tcp_server);
       TcpClient client = new TcpClient();
       client.Connect(uri.Host, uri.Port);*/
       NetworkStream stream = new NetworkStream(sock);
       _sw = new StreamWriter(stream);
    }
    
    start_time = DateTime.Now;
  }

  protected Object logEventLock = new Object(); //This is for logging in connectiontable 
  protected Object logTimeStampLock = new Object(); //This is for logging the packet time stamps
  protected Object BPLock = new Object(); //This is for logging the brunet-ping's
  protected Object PingLock = new Object(); //This is for logging the brunet-ping's


    // temp hack until we get log4net to work on PlanetLab
    public void LogBrunetEvent(BrunetEventDescriptor bed)
    {
      lock(logEventLock) {  

         DateTime CurrTime = DateTime.Now;
         StreamWriter fs = new StreamWriter(_connection_log_file, true);
         fs.WriteLine( CurrTime.ToUniversalTime().ToString("MM'/'dd'/'yyyy' 'HH':'mm':'ss") + 
		                    ":" + CurrTime.ToUniversalTime().Millisecond +
		                    "  " + bed.EventDescription +
				    "  " + bed.ConnectionType + 
				    "  " + bed.RemoteAHAddress + 
				    "  " + bed.SubType); 
         fs.Flush();
         fs.Close();

         if(_net_stream){
	    try{
               _sw.WriteLine( "con_0 " + CurrTime.ToUniversalTime().ToString("MM'/'dd'/'yyyy' 'HH':'mm':'ss") + 
		                    ":" + CurrTime.ToUniversalTime().Millisecond +
		                    "  " + bed.EventDescription +
				    "  " + bed.ConnectionType + 
                                    "  " + this.LocalAHAddress.ToBigInteger().ToString()  + 
				    "  " + bed.RemoteAHAddress + 
				    "  " + bed.SubType); 
               _sw.Flush();
	    }
	    catch(SocketException e){
                Console.WriteLine("Unable to connect to server.");
	        Console.WriteLine(e.ToString());
		this.GracefullyCloseStream();
	    }
         }
      }
    }    

    public void GracefullyCloseStream()
    {
        _sw.Flush();
	_sw.Close();
	sock.Shutdown(SocketShutdown.Both);
	sock.Close();
	Environment.Exit(0);

    }
/**
  * @param p the packet to be logged
  * @param received true if the packet was received, false if the packet was sent, at 
  * the time of logging
  */
    public void LogPacketTimeStamp(Packet p, bool received)
    {       
        TimeSpan elapsed_time = System.DateTime.Now - start_time;
	lock(logTimeStampLock){
            StreamWriter packet_sw = new StreamWriter(_packet_log_file, true);
	    if(received){ 
                packet_sw.WriteLine("{0} \t {1} \t {2} received", elapsed_time.TotalMilliseconds, p.Length, p.type.ToString() );
                packet_sw.Flush(); 	  
            }
            else{
                packet_sw.WriteLine("{0} \t {1} \t {2} sent", elapsed_time.TotalMilliseconds, p.Length, p.type.ToString() );
                packet_sw.Flush(); 	  
            }           
	    packet_sw.Close();
	}
    }

/**
  * @param p the packet to be logged
  * @param received true if the packet was received, false if the packet was sent, at 
  * the time of logging
  */
    public void LogBrunetPing(Packet p, bool received)
    {       
        TimeSpan elapsed_time = System.DateTime.Now - start_time;
        lock(BPLock){	
            StreamWriter bp_sw = new StreamWriter(_brunet_ping_log_file, true);
            if(received){ 
               bp_sw.WriteLine("{0} \t received \t {1}", elapsed_time.TotalMilliseconds, 
                    NumberSerializer.ReadInt(p.PayloadStream.ToArray(), 1) ); //write time, received and uid	 
               bp_sw.Flush(); 
            }
            else{
               bp_sw.WriteLine("{0} \t sent \t \t {1}", elapsed_time.TotalMilliseconds, 
                    NumberSerializer.ReadInt(p.PayloadStream.ToArray(), 1) ); //write time, sent and uid
               bp_sw.Flush(); 
            }	   
	    bp_sw.Close();
	}
    }

/**
  * @param ping_time the icmp ping time in milliseconds
  */
    public void LogPing(double ping_time)
    {      
	lock(PingLock){
            TimeSpan elapsed_time = System.DateTime.Now - start_time;
            StreamWriter icmp_sw = new StreamWriter(_icmp_ping_log_file, true);
	    icmp_sw.WriteLine("{0} \t \t {1}", elapsed_time.TotalMilliseconds, ping_time);
	    icmp_sw.Flush();
	    icmp_sw.Close();
	}
    }

/**
  */
    public void LogBPHeader(String local, short local_port, String target, short target_port)
    {       
        TimeSpan elapsed_time = System.DateTime.Now - start_time;
	lock(BPLock){
            StreamWriter bp_sw = new StreamWriter(_brunet_ping_log_file, true);
	    bp_sw.WriteLine( "local: " + local + ":" + local_port + " remote: " + target + ":" + target_port + " "
                 + DateTime.Now.ToUniversalTime().ToString() + ":" + DateTime.Now.ToUniversalTime().Millisecond); 	        
            bp_sw.Flush(); 
	    bp_sw.Close();
	}
    }

    public void LogPingHeader(String local, short local_port, String target, short target_port)
    {       
        TimeSpan elapsed_time = System.DateTime.Now - start_time;
	lock(PingLock){	      
            StreamWriter icmp_sw = new StreamWriter(_icmp_ping_log_file, true);
	    icmp_sw.WriteLine( "local: " + local + ":" + local_port + " remote: " + target + ":" + target_port + " "
			    + DateTime.Now.ToUniversalTime().ToString() + ":" + DateTime.Now.ToUniversalTime().Millisecond); 	        
            icmp_sw.Flush(); 
	    icmp_sw.Close();
	}
    }

}

}


