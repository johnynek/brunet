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
   * PSWrapper provides a convenient interface to the ps command.
   */
  public class PsWrapper{
  
    private Process _ps_process;
    private int rss; ///ram used measured by rss in KB
    public int Rss{
	get{
		return rss;
	}
    }
    private int vsz; ///ram used measured by vsz in KB
    public int Vsz{
	get{
		return vsz;
	}
    }

     /** Perform ps
     * @return the amount of memory usage in (KB) as measured by RSS     
     */
    public void Ps()
    {
      rss = -1;
      vsz = -1;
      int temp_rss0 = -1, temp_rss1 = -1, temp_vsz0 = -1, temp_vsz1 = -1;
      string prog_string = "ps";
      string arg_string = String.Format("u -C mono");
      //the argument parameters specified as followed: -c is number of pings, 
      _ps_process = new Process();  
      _ps_process.StartInfo.FileName = prog_string;
      _ps_process.StartInfo.Arguments = arg_string;
      _ps_process.StartInfo.UseShellExecute = false;
      _ps_process.StartInfo.RedirectStandardInput = false;
      _ps_process.StartInfo.RedirectStandardOutput = true;
      _ps_process.StartInfo.RedirectStandardError = true;
      _ps_process.Start();
      _ps_process.WaitForExit(800); //we wait 0.8 second
      string input0, input1, input2;
      string[] split_line0;
      string[] split_line1; 
      string[] split_line2; 
      input0 =_ps_process.StandardOutput.ReadLine();
      input1 =_ps_process.StandardOutput.ReadLine();
      input2 =_ps_process.StandardOutput.ReadLine();
      int index = 0, item_index = 0, temp_item_index = 0; //the item_index counter ignores whitespace
///the following section is for getting rss
      if( input0 != null && input0.Length > 0)    
      { 
        split_line0 = input0.Split(new Char[]{' '});
        //parsing the line is a little tricky since
        //the String.Split method in c# splits indiviual equal sign and whitespaceinto
        //seperate string
        while(split_line0[index] != "RSS" && split_line0[index] != "rss" && split_line0[index] != null )
        {
	  if(split_line0[index].Length != 0){
		item_index++;
	  }
	  index++;
        }
      }   
      index = 0;
      if( input1 != null && input1.Length > 0)    
      {	 
        split_line1 = input1.Split(new Char[]{' '});
	while( split_line1[index] != null && temp_item_index <= item_index)
        {
	  if(split_line1[index].Length != 0){
		temp_item_index++;
	  }
	  index++;
        }	
	temp_rss0 = Int32.Parse(split_line1[index-1]);  
      } 
      index = 0;
      temp_item_index = 0;
      if( input2 != null && input2.Length > 0)    
      {	 
        split_line2 = input2.Split(new Char[]{' '});
	while( split_line2[index] != null && temp_item_index <= item_index)
        {
	  if(split_line2[index].Length != 0){
		temp_item_index++;
	  }
	  index++;
        }	
	temp_rss1 = Int32.Parse(split_line2[index-1]);  	
      }   
      rss = Math.Max(temp_rss0, temp_rss1);

///the following section is for getting vsz
      index = 0;
      item_index = 0;
      temp_item_index = 0;
      if( input0 != null && input0.Length > 0)    
      {         
        split_line0 = input0.Split(new Char[]{' '});
	while(split_line0[index] != "VSZ" && split_line0[index] != "vsz" && split_line0[index] != null )
        {
	  if(split_line0[index].Length != 0){
		item_index++;
	  }
	  index++;
        }
      }   
      index = 0;
      if( input1 != null && input1.Length > 0)    
      {	         
	split_line1 = input1.Split(new Char[]{' '});
	while( split_line1[index] != null && temp_item_index <= item_index)
        {
	  if(split_line1[index].Length != 0){
		temp_item_index++;
	  }
	  index++;
        }	
	temp_vsz0 = Int32.Parse(split_line1[index-1]);   	
      }    
      index = 0;
      temp_item_index = 0;
      if( input2 != null && input2.Length > 0)    
      {	        
	split_line2 = input2.Split(new Char[]{' '});
	while( split_line2[index] != null && temp_item_index <= item_index)
        {
	  if(split_line2[index].Length != 0){
		temp_item_index++;
	  }
	  index++;
        }	
	temp_vsz1 = Int32.Parse(split_line2[index-1]);  	
      }   
      vsz = Math.Max(temp_vsz0, temp_vsz1);
    }


    
  }
}