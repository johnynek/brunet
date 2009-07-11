#!/bin/bash
path=`which $0`
path=`dirname $path`
cd $path
mkdir -p /opt/ipop/bin
cp ../bin/*py /opt/ipop/bin/.
cp ../bin/*exe /opt/ipop/bin/.
cp ../bin/*sh /opt/ipop/bin/.

mkdir -p /opt/ipop/lib
cp ../bin/*dll /opt/ipop/lib/.
cp ../bin/*so /opt/ipop/lib/.

mkdir -p /opt/ipop/etc
cp ../config/ipop.vpn.config /opt/ipop/etc/.
cd -

mkdir -p /opt/ipop/var

ln -s /opt/ipop/bin/groupvpn_prepare.sh /usr/sbin/.
ln -s /opt/ipop/bin/ipop_linux.sh /etc/init.d/ipop_linux.sh
ln -s /opt/ipop/etc/ipop.vpn.config /etc/.
