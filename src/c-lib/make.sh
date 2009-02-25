gcc -fPIC -c linux_tap.c
gcc -shared -o libtuntap.so linux_tap.o

