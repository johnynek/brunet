#!/usr/bin/python

import SimpleXMLRPCServer

def main():
  server = SimpleXMLRPCServer.SimpleXMLRPCServer(("0.0.0.0", 8888))
  server.register_instance(SocialNetworkProviderBackend())
  server.serve_forever()
  return

class SocialNetworkProviderBackend:

  def StoreFingerprint(self, uid, fingerprint):
    return "true"

  def GetFingerprints(self, uid):
    fingerprints = []
    fingerprints.append("fpr1");
    fingerprints.append("fpr2");
    return fingerprints

  def GetFriends(self, uid):
    friends = []
    friends.append("friend1");
    friends.append("friend2");
    return friends

  def SayHello(self, name):
    return "Hello " + name

if __name__ == '__main__':
  main()
