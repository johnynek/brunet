using System.Diagnostics;

namespace Ipop {
  public class IpopLog {
    public static BooleanSwitch BaseLog =
        new BooleanSwitch("BaseLog", "Weak level of logging");
    public static BooleanSwitch PacketLog =
        new BooleanSwitch("PacketLog", "Logs incoming and outgoing packets");
    public static BooleanSwitch DHCPLog = 
        new BooleanSwitch("DHCPLog", "Logs DHCP state data");
    public static BooleanSwitch RoutingLog =
        new BooleanSwitch("RoutingLog", "Logs routing data");
    public static BooleanSwitch DNS =
        new BooleanSwitch("DNS", "All about DNS");
  }
}