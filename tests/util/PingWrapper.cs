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

using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Diagnostics;
namespace Brunet{

  /**
   * PingWrapperTracerouteWrapper provides a convenient interface to the ping command.
   */
  public class PingWrapper{
  
    private Process _tr_process;

     /** Perform the ping
     * @param target a string with the address.
     * @param wait_time the number of milliseconds to wait for the process
     * @return the ping roundtrip time in millisecond, if destination is not ping-able, 
     * @a negative value of -1 is returned
     */
    public double Ping(string target, int wait_time)
    {
      string prog_string = "ping";
      string arg_string = String.Format("-c 1 {0}", target);
      //the argument parameters specified as followed: -c is number of pings, 
      _tr_process = new Process();  
      _tr_process.StartInfo.FileName = prog_string;
      _tr_process.StartInfo.Arguments = arg_string;
      _tr_process.StartInfo.UseShellExecute = false;
      _tr_process.StartInfo.RedirectStandardInput = false;
      _tr_process.StartInfo.RedirectStandardOutput = true;
      _tr_process.StartInfo.RedirectStandardError = true;
      _tr_process.Start();
      _tr_process.WaitForExit(wait_time);
      string input;
      string[] split_line; 
      double ping_time = -1.0;
      _tr_process.StandardOutput.ReadLine();
      input =_tr_process.StandardOutput.ReadLine();

      if ( input != null && input.Length > 0)    
      { 
	Console.WriteLine(input);
        int time_index = 0;
        split_line = input.Split(new Char[]{'=', ' '});
        //parsing the line is a little tricky since
        //the String.Split method in c# splits indiviual equal sign and whitespaceinto
        //seperate string
        while(split_line[time_index] != "time" && split_line[time_index] != null)
        {
	  time_index++;	  
        }
	ping_time = double.Parse(split_line[time_index+1]);    
        return ping_time;
      }
      else
      {
        return -1.0;
      }
    }
    
  }
}
