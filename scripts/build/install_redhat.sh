#!/bin/bash
if [[ ! -e `which tunctl 2> /dev/null` ]]; then
  echo "Missing tunctl -- install uml-utilities"
  exit
elif [[ ! -e `which mono 2> /dev/null` ]]; then
  echo "Missing mono -- install mono"
  exit
elif [[ ! -e `which cronolog 2> /dev/null` ]]; then
  echo "Missing cronolog -- install cronolog"
  exit
elif [[ ! -e `which python 2> /dev/null` ]]; then
  echo "Missing python -- install python"
  exit
fi

path=`which $0`
path=`dirname $path`
cd $path &> /dev/null

mkdir -p /opt/ipop/bin
cp ./bin/*py /opt/ipop/bin/.
cp ./bin/*exe /opt/ipop/bin/.
cp ./bin/*sh /opt/ipop/bin/.
cp ./config/Log.config /opt/ipop/bin/DhtIpopNode.exe.config

mkdir -p /opt/ipop/lib
cp ./bin/*dll /opt/ipop/lib/.

mkdir -p /opt/ipop/etc
cp ./config/ipop.vpn.config /opt/ipop/etc/.

cd - &> /dev/null

mkdir -p /opt/ipop/var

ln -sf /opt/ipop/bin/groupvpn_prepare.sh /usr/sbin/.
ln -sf /opt/ipop/bin/groupvpn.sh /etc/init.d/groupvpn.sh
ln -sf /opt/ipop/bin/groupvpn_bootstrap.sh /etc/init.d/groupvpn_bootstrap.sh
ln -sf /opt/ipop/etc/ipop.vpn.config /etc/.

echo "Done installing GroupVPN"
