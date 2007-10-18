using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Sockets;
using System.Threading;
using Brunet;
using Brunet.Dht;

namespace Ipop {
  public class Tracker {
    Thread thread = null;
    private string ip, brunet_addr, key, geo_loc;
    private int count = 0, end = (new Random()).Next(84);

    public Tracker(string key, string ip, string brunet_addr, Dht dht) {
      this.key = key;
      this.ip = ip;
      this.brunet_addr = ip;
      this.geo_loc = ",";
    }

    public void Stop() {
      

    public void RunAsThread() {
      thread = new Thread(Run());
      thread.Start();
    }

    public void Run() {
      int count = 0;
      int restart = (new Random()).Next(84);
      while(true) {
        UpdateTracker();
        Thread.Sleep(1000*60*60);
      }
    }

    public string GeoLoc() {
      try {
        string server = "www.geobytes.com";
        int port = 80;
        Regex lat = new Regex("<td align=\"right\">Latitude.+\r\n.+");
        Regex lon = new Regex("<td align=\"right\">Longitude.+\r\n.+");
        Regex num = new Regex("\\-{0,1}\\d+.\\d+");
        Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        s.Connect(server, port);
        string request = "GET /IpLocator.htm HTTP/1.0\r\nHost: wow.acis.ufl.edu\r\nUser-Agent:  None\r\n\r\n";
        byte[] bs = Encoding.ASCII.GetBytes(request);
        s.Send(bs, bs.Length, 0);
        string page = String.Empty;
        byte[] br = new byte[256];
        int bytes = 0;
        do {
          bytes = s.Receive(br, br.Length, 0);
          page += Encoding.ASCII.GetString(br, 0, bytes);
        } while (bytes > 0);
        Match latm = lat.Match(page);
        Match lonm = lon.Match(page);
        if(latm.Success && lonm.Success) {
          latm = num.Match(latm.Value);
          lonm = num.Match(lonm.Value);
          if(latm.Success && lonm.Success) {
            latm = num.Match(latm.Value);
            lonm = num.Match(lonm.Value);
            geo_loc = latm.Value + ", " + lonm.Value;
          }
        }
      }
      catch {
        geo_loc = ",";
      }
    }

    public void UpdateTracker() {
      if(count == 0 || geo_loc.Equals(", "))
        geo_loc = GeoLoc();
      else if(count == restart)
        count = 0;
      else
        count++;

      while(true) {
        bool result = false;
        try {
          result = dht.Put(key, brunet_addr + "|" + ip + "|" + geo_loc, 7200);
        }
        catch(Exception) {;}
        if(result) {
          break;
        }
        else {
          Thread.Sleep(10000);
        }
      }
    }
  }
}