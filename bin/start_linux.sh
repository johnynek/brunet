#!/bin/bash

# the name email
email=$1

# unique pc identifier
pcid=$2

# the user name
name=${@:3}

# set variables
cert_dir=certificates
root=`whoami`
device=tapipop

if [[ $root != root ]]; then
  echo "Please run as root"
  exit
fi

if [[ ! -f user.txt ]]; then
  echo "Run the following command first (as non-root): whoami > user.txt"
  exit
fi

user=`cat user.txt`

if [[ $user == root ]]; then
  echo "Run the follow command first (AS NON-ROOT): whoami > user.txt"
  exit
fi

./tunctl -d $device
./tunctl -u $user -t $device
chmod 666 /dev/net/tun

if [[ -d $cert_dir ]]; then
  su $user -c "mono SocialVPN.exe brunet.config ipop.config 58888 &> log.txt &"
elif [[ $# -lt 3 ]]; then
  echo "usage (on first run): ./start_linux.sh <email> <pcid> <name>"
  exit
else
  su $user -c "mono SocialVPN.exe brunet.config ipop.config 58888 $email $pcid \"$name\" &> log.txt &"
fi

/sbin/dhclient -pf /var/run/dhclient.$device.pid -lf /var/lib/dhcp3/dhclient.$device.leases $device
