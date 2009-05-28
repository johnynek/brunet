#!/bin/sh

# the name email
email=$1

# unique pc identifier
pcid=$2

# the user name with quotes
name=$3

# the user location
location=$4

user=`whoami`
device=tapipop

sudo tunctl -u $user -t $device
sudo chmod 666 /dev/net/tun

cert_dir=certificates

if [[ -d $cert_dir ]]; then
  mono SocialVPN.exe brunet.config ipop.config 58888 &> log.txt &
else
  if [[ -z $4 ]]; then
    echo "usage (on first run): ./start_linux.sh <email> <pcid> <name> <location>"
    exit
  fi
  mono SocialVPN.exe brunet.config ipop.config 58888 $email $pcid $name $location &> log.txt &
fi

sleep 3
sudo /sbin/dhclient -1 -q -pf /var/run/dhclient.$device.pid -lf /var/lib/dhcp3/dhclient.$device.leases $device
