/*  Not implemented at this time for hints check this site out...
    http://svn.openvpn.net/projects/openvpn/trunk/openvpn/
*/

#include <errno.h>
#include <unistd.h>
#include <sys/ioctl.h>
#include <net/if.h>
#include <linux/if_tun.h>
#include <fcntl.h>

#define ETHER_HEADER_LEN 14
#define MTU 1500

int send_tap(int fd, u_char *packet, int len) {
}

int read_tap(int fd, void *packet, int len) {
}

int open_tap(char *dev) {
}
