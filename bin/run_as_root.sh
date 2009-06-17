#!/bin/bash

# Username
user=$1

# set variables
root=`whoami`
device=tapipop

if [[ $root != "root" ]]; then
  echo "Please run as root"
  exit
elif [[ $# -lt 1 ]]; then
  echo "Please provide user name"
  exit
fi

tunctl -d $device
tunctl -u $user -t $device
chmod 666 /dev/net/tun

/sbin/dhclient -nw -pf /var/run/dhclient.$device.pid -lf /var/lib/dhcp3/dhclient.$device.leases $device
