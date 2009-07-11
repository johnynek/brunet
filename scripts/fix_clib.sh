#!/bin/bash
path=`which $0`
path=`dirname $path`
cd $path/drivers/c-lib
./make.sh
cp libtuntap.so ../../bin/.
cd -
