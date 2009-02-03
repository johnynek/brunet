#include <errno.h>
#include <unistd.h>
#include <sys/ioctl.h>
#include <net/if.h>
#include <linux/if_tun.h>
#include <fcntl.h>

int send_tap(int fd, u_char *packet, int len) {
  return write(fd, packet, len);
}

int read_tap(int fd, void *packet, int len) {
  return read(fd, packet, len);
}

int open_tap(char *dev) {
  struct ifreq ifr;
  int fd, err;
  if((fd = open("/dev/net/tun", O_RDWR)) < 0){
    perror("Failed to open /dev/net/tun");
    return -1;
  }
  memset(&ifr, 0, sizeof(ifr));
  ifr.ifr_flags = IFF_TAP | IFF_NO_PI;
  strncpy(ifr.ifr_name, dev, sizeof(ifr.ifr_name) - 1);
  if(ioctl(fd, TUNSETIFF, (void *) &ifr) < 0){
    perror("TUNSETIFF failed");
    close(fd);
    return -1;
  }
  return(fd);
}

int close_tap(int fd) {
  return close(fd);
}

int get_hw_addr(int fd, void *dev) {
  struct ifreq ifr;
  memset(&ifr, 0, sizeof(ifr));
  if(ioctl(fd, SIOCGIFHWADDR, &ifr) < 0) {
    perror("Failled to get hw addr.");
    return -1;
  }
  memcpy(dev, &(ifr.ifr_hwaddr.sa_data), 6);
  return 0;
}

