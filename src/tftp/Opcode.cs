namespace Brunet.Tftp {

/**
 * Represents the types of operations in Tftp
 * @see http://www.faqs.org/rfcs/rfc1350.html
 * @see http://www.faqs.org/rfcs/rfc1782.html
 * 
 */
public enum Opcode : short {

  ReadReq  = 1,
  WriteReq = 2,
  Data = 3,
  Ack = 4,
  Error = 5,
  OptionAck = 6
}
	
}
