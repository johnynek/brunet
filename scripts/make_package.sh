cd ..
main_dir=`pwd`
cd src
cd c-lib
nant -t:mono-1.0
cd ..
cd common
nant -t:mono-1.0
cd ..
cd dhcp
nant -t:mono-1.0
cd build
mkbundle --static --deps --config-dir . -o dhcpserver DHCPServer.exe DHCPCommon.dll
cd ../..
cd ipop
nant -t:mono-1.0
cd build
mkdir -p mono/1.0
mkbundle --static --deps --config-dir . -o iprouter IPRouter.exe DHCPCommon.dll Brunet.dll Ipop-common.dll /usr/local/lib/mono/1.0/Mono.Posix.dll
cd ../../..
mkdir packages
cd packages
zip -j9 IPRouter_mono.zip ../src/ipop/build/IPRouter.exe ../src/ipop/build/DHCPCommon.dll ../src/ipop/build/Brunet.dll ../src/ipop/build/libtuntap.so ../docs/readme
zip -j9 IPRouter_mkbundle.zip ../src/ipop/build/iprouter ../src/ipop/build/libtuntap.so ../docs/readme /etc/mono/1.0/machine.config
zip -j9 DHCPServer_mono.zip ../src/dhcp/build/DHCPServer.exe ../src/dhcp/build/DHCPCommon.dll ../docs/readme
zip -j9 DHCPServer_mkbundle.zip ../src/dhcp/build/dhcpserver ../docs/readme
echo "Packages made"
cd ../scripts
