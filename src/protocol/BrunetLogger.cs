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
 */
using System;
using System.Collections;
using System.IO;
using System.Globalization;
using System.Xml;
using System.Text;
using System.Xml.Serialization;

namespace Brunet{

public class BrunetLogger
{

  private String _log_file;
  private static DateTime start_time;
  
  [XmlAttribute("LogFile")] 
  public string LogFile
  {
    get{
      return _log_file;
    }
    set
    {
      _log_file = value;
    }
  }	 

  public BrunetLogger(){
    start_time = DateTime.Now;
  }
  
  public BrunetLogger(String logfile)
  {
    _log_file = logfile;
    start_time = DateTime.Now;
  }

  protected Object logEventLock = new Object(); //This is for logging in connectiontable 
  protected Object logCTMLock = new Object(); //This is for logging connect to message events
  protected static Object logTimeStampLock = new Object(); //This is for logging the packet time stamps
  protected static Object logRDPLock = new Object(); //This is for logging the ping and brunet-ping's

    // temp hack until we get log4net to work on PlanetLab
    public void LogBrunetEvent(BrunetEventDescriptor bed)
    {
      lock(logEventLock) {  
      /*String filepath = "events.log";

      FileStream fs = File.Open(filepath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
      XmlTextWriter writer = new XmlTextWriter(fs, Encoding.ASCII);

      writer.WriteElementString("event", "",
	    DateTime.Now.ToUniversalTime().ToString() + "  " + bed.EventDescription +
				    "  " + bed.ConnectionType + 
				    "  " + bed.LocalTAddress + 
				    "  " + bed.RemoteTAddress );
      writer.WriteWhitespace("\n");
      writer.Close();*/


      String addfile = _log_file;
      StreamWriter sw = new StreamWriter(addfile, true);
      sw.WriteLine( DateTime.Now.ToUniversalTime().ToString() + ":" + DateTime.Now.ToUniversalTime().Millisecond +
		                    "  " + bed.EventDescription +
				    "  " + bed.ConnectionType + 
				    "  " + bed.RemoteAHAddress + 
				    "  " + bed.SubType); 
      sw.Close();

      ///logging of connection events is moved to Linker.cs and ConnectionPacketHandler.cs

      }
    }    

    public void LogCTMEvent(Address target)
    {
      lock(logCTMLock) {  

      String ctmfile = "./data/ctm.log";
      StreamWriter sw = new StreamWriter(ctmfile, true);
      sw.WriteLine( DateTime.Now.ToUniversalTime().ToString() + " CTM " 
				    + "Structured " + 
				    + target.ToBigInteger().ToString() ); 
      sw.Close();

      }
    }

/**
  * @param p the packet to be logged
  * @param received true if the packet was received, false if the packet was sent, at 
  * the time of logging
  */
    public static void LogPacketTimeStamp(Packet p, bool received)
    {       
        TimeSpan elapsed_time = System.DateTime.Now - start_time;
	lock(logTimeStampLock){
	StreamWriter stamp_sw = new StreamWriter("time_stamp.log", true);
	    if(elapsed_time.TotalSeconds < 3000){ ///we log for the first 3000 seconds
		if(received){ 
		  stamp_sw.WriteLine("{0} \t {1} \t {2} received", elapsed_time.TotalMilliseconds, p.Length, p.type.ToString() );
		}
		else{
		  stamp_sw.WriteLine("{0} \t {1} \t {2} sent", elapsed_time.TotalMilliseconds, p.Length, p.type.ToString() );
		}
		stamp_sw.Close(); 
	    }     
	}
    }

/**
  * @param p the packet to be logged
  * @param received true if the packet was received, false if the packet was sent, at 
  * the time of logging
  */
    public static void LogBrunetPing(Packet p, bool received)
    {       
        TimeSpan elapsed_time = System.DateTime.Now - start_time;
	lock(logRDPLock){
	    StreamWriter bp_sw = new StreamWriter("brunet-ping.log", true);	    
	    if(elapsed_time.TotalSeconds < 80000){ ///for time-out
		if(received){ 
		  bp_sw.WriteLine("{0} \t received \t {1}", elapsed_time.TotalMilliseconds, 
			NumberSerializer.ReadInt(p.PayloadStream.ToArray(), 1) ); //write time, received and uid
		}
		else{
		  bp_sw.WriteLine("{0} \t sent \t \t {1}", elapsed_time.TotalMilliseconds, 
			NumberSerializer.ReadInt(p.PayloadStream.ToArray(), 1) ); //write time, sent and uid
		}		 
	    }    
            bp_sw.Close(); 
	}
    }

/**
  * @param ping_time the icmp ping time in milliseconds
  */
    public static void LogPing(double ping_time)
    {       
        TimeSpan elapsed_time = System.DateTime.Now - start_time;
	StreamWriter icmp_sw = new StreamWriter("icmp-ping.log", true);	    
	icmp_sw.WriteLine("{0} \t \t {1}", elapsed_time.TotalMilliseconds, ping_time);
	icmp_sw.Close();
    }

/**
  */
    public static void LogBPHeader(String local, String target)
    {       
        TimeSpan elapsed_time = System.DateTime.Now - start_time;
	lock(logRDPLock){
	    StreamWriter bp_sw = new StreamWriter("brunet-ping.log", true);	    
	    if(elapsed_time.TotalSeconds < 80000){ ///for time-out
		    bp_sw.WriteLine( "local: " + local + " remote: " + target + " "
	              + DateTime.Now.ToUniversalTime().ToString() + ":" + DateTime.Now.ToUniversalTime().Millisecond); 
	    }    
            bp_sw.Close(); 
	}
    }

    public static void LogPingHeader(String local, String target)
    {       
        TimeSpan elapsed_time = System.DateTime.Now - start_time;
	lock(logRDPLock){
	    StreamWriter icmp_sw = new StreamWriter("icmp-ping.log", true);	    
	    if(elapsed_time.TotalSeconds < 80000){ ///for time-out
		    icmp_sw.WriteLine( "local: " + local + " remote: " + target + " " 
	              + DateTime.Now.ToUniversalTime().ToString() + ":" + DateTime.Now.ToUniversalTime().Millisecond); 
	    }    
            icmp_sw.Close(); 
	}
    }



}

}


