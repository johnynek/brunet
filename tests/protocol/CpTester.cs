/*
 * using Brunet.ConnectionMessageParser;
 * using Brunet.ConnectionType;
 * using Brunet.ConnectionMessage;
 * using Brunet.ConnectToMessage;
 * Brunet.ErrorMessage
 * using Brunet.LinkMessage;
 * using Brunet.CloseMessage;
 * using Brunet.TransportAddress;
 * using Brunet.AHAddress;
 * using Brunet.Address;
 * using Brunet.PingMessage;
 */

using Brunet;
using System;
using System.Xml;

namespace Brunet
{

  /**
   * Simple test class to test the writing and parsing of
   * ConnectionMessages
   */
  public class CpTester
  {

    public static void Main()
    {
      ConnectionMessageParser p = new ConnectionMessageParser();

        ConnectionMessage[]  cm = new ConnectionMessage[5];
      CloseMessage close = new CloseMessage();
        close.Dir = ConnectionMessage.Direction.Response;
        close.Id = 237;
        cm[0] = close;
      XmlWriter w = new XmlTextWriter(Console.OpenStandardOutput(),
                                      new System.Text.UTF8Encoding());

        close.WriteTo(w);

      LinkMessage link = new LinkMessage();

        link.Dir = ConnectionMessage.Direction.Request;
        link.ConnectionType = ConnectionType.Structured;
        link.LocalTA =
        new TransportAddress("brunet.tcp://127.0.0.1:23");
        link.RemoteTA =
        new TransportAddress("brunet.tcp://127.0.0.1:55");
        link.LocalNode = new AHAddress(new byte[20]);
        cm[1] = link;

        link.WriteTo(w);

      ConnectToMessage ctm = new ConnectToMessage();
        ctm.TargetAddress = new AHAddress(new byte[20]);
	TransportAddress[] ta = new TransportAddress[2];
	ta[0] = link.LocalTA;
	ta[1] = link.RemoteTA;
        ctm.TransportAddresses = ta;
        cm[2] = ctm;

        ctm.WriteTo(w);

      PingMessage ping = new PingMessage();
        cm[3] = ping;
        ping.WriteTo(w);

      ErrorMessage em = new ErrorMessage(ErrorMessage.ErrorCode.AlreadyConnected,
		                         "already test");
      cm[4] = em;
      em.WriteTo(w);
      w.Flush();
//About to parse messages : 
        Console.Out.WriteLine("About to parse messages: ");
	Console.Out.WriteLine();
        foreach(ConnectionMessage mes in cm)
      {
        ConnectionMessage m1 = p.Parse(mes.ToByteArray());
        if (m1 != null)
        {
          Console.Out.WriteLine("Parsed: {0}", m1.ToString());
	  Console.Out.WriteLine();
          m1.WriteTo(w);
	  Console.Out.WriteLine();
        }
        else
        {
          Console.Error.WriteLine("Failed to parse: {0}",
                                mes.ToString());
        }
      }
      w.Flush();
      w.Close();
    }

  }

}
