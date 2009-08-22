#!/usr/bin/python

import xmlrpclib
import unittest

class XmlRpcManagerTestCase(unittest.TestCase):

  def testExternalXmlRpcService(self):
    """ Needs BasicNode and xmlrpcserver running """
    # 10000 is the default port for XmlRpcManager
    url = 'http://127.0.0.1:10000/xm.rem'
    rpc = xmlrpclib.Server(url)
    # You have a server named 'host' at http://localhost:8000 that has the get_stats method
    try:
      rpc.localproxy("xmlrpc.AddXRHandler", 'host','http://127.0.0.1:8000')
      # Reigstered service at http://127.0.0.1:8000 with the name host. Listening...
    except xmlrpclib.Fault:
      #do nothing
      pass
    # Calling function no_arg on host
    self.assertEqual('Hello World!', rpc.localproxy('host.no_arg'))
    # Calling function add on host
    self.assertEqual(3, rpc.localproxy('host.add', 1, 2))
    # Calling function wait_and_return on host
    rpc.localproxy('host.wait_and_return', 5)
    # Removing handler"
    rpc.localproxy("xmlrpc.RemoveXRHandler", 'host','http://127.0.0.1:8000')
    # Calling again...
    self.assertRaises(xmlrpclib.Fault, rpc.localproxy, 'host.add', 1, 2)

  def testServerUrls(self):
    """ Needs BasicNode running """
    defaultUrl = 'http://127.0.0.1:10000/xm.rem'
    serverUrl = 'http://127.0.0.1:10000/xmserver.rem'
    rpc = xmlrpclib.Server(defaultUrl)
    info = rpc.localproxy("Information.Info")
    rpc = xmlrpclib.Server(serverUrl)
    nodeList = rpc.listNodes()
    self.assertEqual(info['neighbors']['self'], nodeList[0])
    addressUrl = "http://127.0.0.1:10000/%s.rem" % nodeList[0]
    rpc = xmlrpclib.Server(addressUrl)
    info1 = rpc.localproxy("Information.Info")
    self.assertEqual(info['neighbors']['self'], info1['neighbors']['self'])

if __name__ == '__main__':
  suite = unittest.TestLoader().loadTestsFromTestCase(XmlRpcManagerTestCase)
  unittest.TextTestRunner(verbosity=1).run(suite)