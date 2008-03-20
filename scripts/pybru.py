#!/usr/bin/env python

import base64

#for testing
import unittest, random, struct

def int_to_bytes(x, bytes):
  """Converts an integer to a msb first byte string of length bytes"""
  #I wish I could figure out a way to express the next four lines
  #a list comprehension, can you?
  bindata = []
  for i in xrange(bytes):
    (x, m) = divmod(x, 256)
    bindata.append(m)
  bindata.reverse()
  return "".join(map(chr, bindata))

def bytes_to_int(bindata):
  """Convert a sequence of bytes into a number"""
  return reduce(lambda x,y: (x<<8) | y, map(ord,bindata), 0)

class Address(object):
  def __init__(self, arg):
    try:
      self.num = long(arg)
      self.bindata = int_to_bytes(self.num, 20)
      self.str = 'brunet:node:' + base64.b32encode(self.bindata)
    except:
      s = str(arg)
      assert s.startswith('brunet:node:'), s
      self.bindata = base64.b32decode( s[12:44] )
      self.str = s[0:44]
      self.num = bytes_to_int(self.bindata)

  def __cmp__(self, other):
    if isinstance(other, Address):
      return cmp(self.num, other.num)
    else:
      return self.__cmp__(Address(other))

  def __long__(self):
    return self.num
  def __str__(self):
    return self.str
    


#############################
# Here are the unit tests
#############################

class TestPyBru(unittest.TestCase):
  def testbin(self):
    """Does a round trip unit test"""
    for i in xrange(1000):
      r = random.randint(0, 2 ** 160 - 1)
      bytes = int_to_bytes(r, 20)
      r2 = bytes_to_int(bytes)
      self.assertEqual(r,r2)
  def testbinstruct(self):
    """Tests against the struct module"""
    for i in xrange(1000):
      r = random.randint(0, 2 ** 32 - 1)
      r2 = bytes_to_int(struct.pack(">I", r))
      self.assertEqual(r, r2)
  def testAddressRoundTrip(self):
    for i in xrange(1000):
      r = random.randint(0, 2**160 - 1)
      a = Address(r)
      self.assertEqual(a, r)
      self.assertEqual(a, str(a))
      self.assertEqual(a, Address(long(a)))
      self.assertEqual(a, Address(str(a)))
  def testAddresscmp(self):
    for i in xrange(1000):
      r1 = random.randint(0, 2**160 - 1)
      a1 = Address(r1)
      r2 = random.randint(0, 2**160 - 1)
      a2 = Address(r2)
      self.assertEqual(a1 < a2, r1 < r2)
      self.assertEqual(a1 > a2, r1 > r2)
      self.assertEqual(a1 == a2, r1 == r2)

if __name__ == '__main__':
  unittest.main()
