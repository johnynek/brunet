
# This is a simple boo script using Brunet.
#

import Brunet
import System
import System.Security.Cryptography


class BoonetChat (IAHPacketHandler):
  _node as Node
  def constructor(port as int, remote as string):
    rng = RNGCryptoServiceProvider()
    tmp_add = AHAddress(rng)
    print tmp_add
    _node = StructuredNode(tmp_add)
    _node.AddEdgeListener( TcpEdgeListener(port) )
    _node.RemoteTAs.Add( TransportAddress( remote ) )
    _node.Subscribe(AHPacket.Protocol.Chat, self);
    _node.Connect();

  #These are for the IAHPacketHandler interface:
  def HandleAHPacket(node as object, p as AHPacket, from_edge as Edge):
    print p
  def HandlesAHProtocol(type as AHPacket.Protocol) as bool:
    return true;
  def send(to as string, msg as string):
    _node.SendTo( AddressParser.Parse(to), AHPacket.Protocol.Chat, \
                  System.Text.Encoding.UTF8.GetBytes(msg))

print "Welcome to BoonetChat"
port = Int32.Parse(prompt("What port to listen on?"))
ta = prompt("What remote ta?")

chatter = BoonetChat(port, ta)

while true:
  who = prompt("Who to send?")
  what = prompt("type your message:")
  chatter.send(who, what)
