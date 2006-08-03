from SocketServer import UDPServer, ThreadingUDPServer, DatagramRequestHandler
import array, string, socket

class DNSServer(ThreadingUDPServer):
  def __init__(self):
    ThreadingUDPServer.__init__(self, ("0.0.0.0", 53), DNSRequestHandler)

class DNSRequestHandler (DatagramRequestHandler):
  def setup(self):
    self.packet, self.socket = self.request
    self.data = array.array('B', self.packet)
    self.ip = [0, 0, 0, 0]
    current = 12
    while self.data[current] != 0:
      current += self.data[current] + 1
    self.qtype = (int(self.data[current+1]) << 8) + int(self.data[current+2])

  def handle(self):
    bad = 1
    if self.check_header() == 1:
      if self.check_name_node():
        bad = 0
      elif self.check_name_arpa():
        bad = 0

    if bad == 0:
      if(self.ip[0] == 10 and self.ip[1] > 127 and self.ip[1] < 256 and \
        self.ip[2] < 256 and self.ip[3] < 255 and self.ip [3] > 0):
        if self.qtype == 12:
          self.add_answer_ptr()
        else:
          self.add_answer_host()
      else:
        bad = 1

    if bad == 1:
    #reject request since we don't handle these types
      self.data[3] = self.data[3] | 5

  def finish(self):
    # We are responding to a query
    self.data[2] = self.data[2] | 0x80
    self.socket.sendto(self.data.tostring(), self.client_address)

  def check_header(self):
    qr = (self.data[2] & 0x80) >> 7
    opcode = (self.data[2] & 0x78) >> 3
    qdcount = (self.data[4] << 8) + self.data[5]
    ancount = (self.data[6] << 8) + self.data[7]
    nscount = (self.data[8] << 8) + self.data[9]
    arcount = (self.data[10] << 8) + self.data[11]

    if (qr == 0) and (opcode == 0) and (qdcount == 1) and (ancount == 0) and \
      (nscount == 0) and (arcount == 0):
      return True
    return False

  def check_name_node(self):
    #starts with a c or a C and is 9 more characters
    test_char = chr(self.data[13])
    if ((test_char == 'c') or (test_char == 'C')) and self.data[12] == 10:
      if self.data[14:23].tostring().isdigit():
        self.ip = [10, int(self.data[14:17].tostring()), \
          int(self.data[17:20].tostring()), \
          int(self.data[20:23].tostring())]
        return True
    return False

  def check_name_arpa(self):
    bad = 1
    current = 12
    parsed_data = []
    while self.data[current] != 0:
      temp = ""
      for i in (range(1,self.data[current]+1)):
        temp += chr(self.data[i + current])
      parsed_data.append(temp)
      current += self.data[current] + 1

    if len(parsed_data) > 5:
      for i in range(4):
        if(parsed_data[i].isdigit == False):
          break;
        else:
          self.ip[(i-3)*-1] = int(parsed_data[i])
        if (i == 3):
          return True
    return False

  def add_answer_host(self):
    #We now have an answer
    self.data[7] = 1
    #We are the Authoritative Answer
    self.data[2] = self.data[2] | 4
    #Name - point (pointer = 12) to the previous string (offset = 12)
    self.data.append(0xC0)
    self.data.append(12)
    #Type
    self.data.append(0)
    self.data.append(1)
    #Class - Internet
    self.data.append(0)
    self.data.append(1)
    #TTL - Max
    for i in range(4):
      self.data.append(127)
    #RDLENGTH
    self.data.append(0)
    self.data.append(4)
    #RDATA - ARPA Internet Address
    self.data.append(self.ip[0])
    self.data.append(self.ip[1])
    self.data.append(self.ip[2])
    self.data.append(self.ip[3])

  def add_answer_ptr(self):
    #We now have an answer
    self.data[7] = 1
    #We are the Authoritative Answer
    self.data[2] = self.data[2] | 4

    #Name - point (pointer = 12) to the previous string (offset = 12)
    self.data.append(0xC0)
    self.data.append(12)
    #Type
    self.data.append(0)
    self.data.append(12)
    #Class - Internet
    self.data.append(0)
    self.data.append(1)
    #TTL - Max
    for i in range(4):
      self.data.append(127)
    temp = "C"
    for i in range(1,4):
     if self.ip[i] < 10:
       temp += "00"
     elif self.ip[i] < 100:
       temp += "0"
     temp += str(self.ip[i])
    domainname = "ipop"
    #RDLENGTH
    self.data.append(0)
    self.data.append(len(temp) + len(domainname) + 3)
    self.data.append(len(temp))
    for i in range(len(temp)):
      self.data.append(ord(temp[i]))
    self.data.append(len(domainname))
    for i in range(len(domainname)):
      self.data.append(ord(domainname[i]))
    self.data.append(0)

def main():
  server = DNSServer()
  server.serve_forever()

if __name__ == "__main__":
  main()