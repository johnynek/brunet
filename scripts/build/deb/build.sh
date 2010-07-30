#!/bin/bash
VERSION=`cat ../version`
DISTRIBUTIONS="ubuntu9.04"
for DISTRIBUTION in $DISTRIBUTIONS; do
  export PACKAGE_DIR=ipop\_$VERSION\_$DISTRIBUTION\_all
  DEBIANDIR=$PACKAGE_DIR/DEBIAN

  mkdir -p $PACKAGE_DIR/etc/init.d
  mkdir -p $PACKAGE_DIR/usr/sbin

  ../install_debian.sh

  mkdir -p $PACKAGE_DIR/DEBIAN
  cp control_$DISTRIBUTION $DEBIANDIR/control
  version=$VERSION"_"$DISTRIBUTION
  echo "Version: $version" >> $DEBIANDIR/control
#  cp conffiles $DEBIANDIR/.

  dpkg-deb -b $PACKAGE_DIR
  rm -rf $PACKAGE_DIR
done
