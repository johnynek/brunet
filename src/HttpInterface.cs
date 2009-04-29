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

namespace SocialVPN
{
  public class HttpInterface
  {
    public const string JSURL = "socialvpn.js";

    public const string URL = "http://127.0.0.1:58888/";

    public event EventHandler ProcessEvent;

    public string StateXml;

    protected HttpListener _listener;
    
    protected Thread _runner;

    protected bool _running;

    public HttpInterface()
    {
      _listener = new HttpListener();
      _listener.Prefixes.Add(URL);
      _runner = new Thread(Run);
      _runner.IsBackground = true;
      _running = false;
    }


    public static string CrossDomainXML
    {
      get
      {
        return "<?xml version=\"1.0\"?>" +
           "<!DOCTYPE cross-domain-policy SYSTEM " +
           "\"http://www.macromedia.com/xml/dtds/cross-domain-policy.dtd\">" +
           "<cross-domain-policy>" +
           "<allow-access-from domain=\"*\" />" +
           "</cross-domain-policy>";
      }
    }

    public static string HTMLText
    {
      get
      {
        return "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" " +
       "\"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\">" +
       "<html><head></head><body><script type=\"text/javascript\" src=\""
       + JSURL + "\"></script></body></html>";
      }
    }

    protected string Process(Object obj)
    {
      EventHandler processEvent = ProcessEvent;
      if (processEvent != null)
      {
        try
        {
          processEvent(obj, EventArgs.Empty);
        }
        catch (Exception e)
        {
          Console.WriteLine(e);
        }
      }
      return StateXml;
    }

    public void Start()
    {
      if(_running) {
        return;
      }
      _running = true;
      _listener.Start();
      _runner.Start();
    }

    public void Stop()
    {
      _running = false;
      _listener.Stop();
    }

    /// </summary>
    protected void Run()
    {
      while(_running)
      {
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
        response.ContentType = "text/xml";

        // Process api post request from the Javascript interface
        if (request.RawUrl == "/api")
        {
          StreamReader reader = new StreamReader(request.InputStream,
                                                 request.ContentEncoding);

          string postData = reader.ReadToEnd();
          request.InputStream.Close();
          reader.Close();
          responseString = Process(SocialUtils.DecodeUrl(postData));
        }
        else if (request.RawUrl.StartsWith("/getapi"))
        {
          string getData = request.RawUrl.Substring(8);
          responseString = Process(SocialUtils.DecodeUrl(getData));
        }
        // Cross-domain request made by Flash clients
        else if (request.RawUrl == "/crossdomain.xml")
        {
          responseString = CrossDomainXML;
        }
        else if (request.RawUrl == "/socialvpn.js")
        {
          using (StreamReader text = new StreamReader("socialvpn.js"))
          {
            responseString = text.ReadToEnd();
          }
          response.ContentType = "text/javascript";
        }
        else if (request.RawUrl == "/socialvpn.css")
        {
          using (StreamReader text = new StreamReader("socialvpn.css"))
          {
            responseString = text.ReadToEnd();
          }
          response.ContentType = "text/css";
        }
        // Return html content for page display
        else
        {
          responseString = HTMLText;
          response.ContentType = "text/html";
        }

        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        response.AddHeader("Cache-Control", "No-cache");
        System.IO.Stream output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
        output.Close();
      }
    }
  }
}
