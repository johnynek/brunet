#include <errno.h>
#include <unistd.h>
#include <sys/ioctl.h>
#include <net/if.h>
#include <linux/if_tun.h>
#include <fcntl.h>

#define ETHER_HEADER_LEN 14
#define MTU 1500

int send_tap(int fd, u_char *packet, int len) {
  //fprintf(stdout, "Sending a packet of length: %d\n", len);
  int n;
  n = write(fd, packet, len);
  return n;
}

int read_tap(int fd, void *packet, int len) {
  int n;
  n = read(fd, packet, len);
  return n;
}

int open_tap(char *dev)
{
  struct ifreq ifr;
  int fd, err;
                                                                                                                             
  if((fd = open("/dev/net/tun", O_RDWR)) < 0){
    perror("Failed to open /dev/net/tun");
    return(-1);
  }
  memset(&ifr, 0, sizeof(ifr));
  ifr.ifr_flags = IFF_TAP | IFF_NO_PI;
  strncpy(ifr.ifr_name, dev, sizeof(ifr.ifr_name) - 1);
  if(ioctl(fd, TUNSETIFF, (void *) &ifr) < 0){
    perror("TUNSETIFF failed");
    close(fd);
    return(-1);
  }
  return(fd);
}
