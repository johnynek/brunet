#!/bin/bash
#PACKAGE_DIR is used for creating a Debian / Ubuntu package and has no effect
#when it is not instantiated

if [[ ! $PACKAGE_DIR ]]; then
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
fi

path=`which $0`
path=`dirname $path`

mkdir -p $PACKAGE_DIR/opt/ipop/bin
cp $path/bin/*py $PACKAGE_DIR/opt/ipop/bin/.
cp $path/bin/*exe $PACKAGE_DIR/opt/ipop/bin/.
cp $path/bin/*sh $PACKAGE_DIR/opt/ipop/bin/.
cp $path/config/Log.config $PACKAGE_DIR/opt/ipop/bin/DhtIpopNode.exe.config

mkdir -p $PACKAGE_DIR/opt/ipop/lib
cp $path/bin/*dll* $PACKAGE_DIR/opt/ipop/lib/.

mkdir -p $PACKAGE_DIR/opt/ipop/etc
cp $path/config/ipop.vpn.config $PACKAGE_DIR/opt/ipop/etc/.

mkdir -p $PACKAGE_DIR/opt/ipop/var

cd $PACKAGE_DIR/usr/sbin
ln -sf ../../opt/ipop/bin/groupvpn_prepare.sh .
cd - &> /dev/null
cd $PACKAGE_DIR/etc/init.d
ln -sf ../../opt/ipop/bin/groupvpn.sh .
ln -sf ../../opt/ipop/bin/groupvpn_bootstrap.sh .
cd - &> /dev/null
cd $PACKAGE_DIR/etc
ln -sf ../opt/ipop/etc/ipop.vpn.config .
cd - &> /dev/null

if [[ ! $PACKAGE_DIR ]]; then
  echo "Done installing GroupVPN"
fi
