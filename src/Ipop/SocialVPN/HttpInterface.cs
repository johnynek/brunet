/*
Copyright (C) 2008 Pierre St Juste <ptony82@ufl.edu>, University of Florida

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
using System.Collections.Generic;
using System.Net;
using System.Web;
using System.IO;
using System.Threading;

using Brunet;

namespace Ipop.SocialVPN {

  public class HttpInterface {
    protected const string JSURL = "socialvpn.js";

    protected const string JSURLDNS = "socialdns.js";

    public event EventHandler ProcessEvent;

    protected readonly HttpListener _listener;
    
    protected readonly Thread _runner;

    protected bool _running;

    public HttpInterface(string port) {
      _listener = new HttpListener();
      _listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
      _runner = new Thread(Run);
      _runner.IsBackground = true;
      _running = false;
    }

    protected static string GetHtmlText(string url) {
      return "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" " +
       "\"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\">" +
       "<html><head><script type=\"text/javascript\" src=\""
       + url + "\"></script></head></html>";
    }

    protected string Process(Dictionary<string, string> request) {
      EventHandler process_event = ProcessEvent;
      string response = String.Empty;
      if (process_event != null) {
        try {
          process_event(request, EventArgs.Empty);
          response = request["response"];
        } catch (Exception e) {
          response = e.ToString();
        }
      }      
      return response;
    }

    public void Start() {
      if(_running) {
        return;
      }
      _running = true;
      _listener.Start();
      _runner.Start();
    }

    public void Stop() {
      _running = false;
      _listener.Stop();
      _runner.Join();
    }

    protected void Run() {
      while(_running) {
        HttpListenerContext context = null;

        try {
          context = _listener.GetContext();
        } catch(HttpListenerException e) {
          if(!_running) {
            return;
          }
          Console.WriteLine(e);
        }
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;
        string responseString = String.Empty;
        byte[] buffer = null;
        response.ContentType = "text/xml";

        if (request.RawUrl == "/state.xml") {
          StreamReader reader = new StreamReader(request.InputStream,
                                                 request.ContentEncoding);

          string postData = reader.ReadToEnd();
          request.InputStream.Close();
          reader.Close();
          
          ProtocolLog.WriteIf(SocialLog.SVPNLog, String.Format(
                              "HTTP API: {0} {1}",
                              DateTime.Now.TimeOfDay, postData));
          
          responseString = Process(SocialUtils.DecodeUrl(postData));
        }
        else if (request.RawUrl == "/socialvpn.js") {
          using (StreamReader text = new StreamReader("socialvpn.js")) {
            responseString = text.ReadToEnd();
          }
          response.ContentType = "text/javascript";
        }
        else if (request.RawUrl == "/socialvpn.css") {
          using (StreamReader text = new StreamReader("socialvpn.css")) {
            responseString = text.ReadToEnd();
          }
          response.ContentType = "text/css";
        }
        else if (request.RawUrl == "/socialdns.js") {
          using (StreamReader text = new StreamReader("socialdns.js")) {
            responseString = text.ReadToEnd();
          }
          response.ContentType = "text/javascript";
        }
        else if (request.RawUrl == "/socialdns.css") {
          using (StreamReader text = new StreamReader("socialdns.css")) {
            responseString = text.ReadToEnd();
          }
          response.ContentType = "text/css";
        }
        else if (request.RawUrl == "/sdns") {
          responseString = GetHtmlText(JSURLDNS);
          response.ContentType = "text/html";
        }
        else {
          responseString = GetHtmlText(JSURL);
          response.ContentType = "text/html";
        }

        if ( buffer == null && responseString != null) {
          buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
        }
        response.ContentLength64 = buffer.Length;
        response.AddHeader("Cache-Control", "No-cache");
        System.IO.Stream output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
        output.Close();
      }
    }
  }
}
