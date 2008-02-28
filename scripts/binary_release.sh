#!/bin/bash
version=$1
mkdir -p $version/ipop.src
mkdir $version/tmp
hg clone ../../ipop $version/tmp/ipop_tmp
hg clone ../../brunet1 $version/tmp/brunet1_tmp
mkdir $version/ipop.src/ipop
mkdir $version/ipop.src/brunet1
cp -axf $version/tmp/ipop_tmp/* $version/ipop.src/ipop/.
cp -axf $version/tmp/brunet1_tmp/* $version/ipop.src/brunet1/.

cd ..
nant
cd -

mkdir -p $version/ipop/bin
brunet_lib_files="Brunet.dll Brunet.Dht.dll Brunet.DhtServices.dll Brunet.XmlRpc.dll"
brunet_bin_files="BasicNode.exe MultiNode.exe"
ipop_bin_files="DhtIpopNode.exe CondorIpopNode.exe"
ipop_lib_files="libtuntap.dll libtuntap.so CookComputing.XmlRpcV2.dll"
for file in $brunet_lib_files; do 
  cp ../../brunet1/lib/$file $version/ipop/bin/.
done

for file in $brunet_bin_files; do 
  cp ../../brunet1/bin/$file $version/ipop/bin/.
done

for file in $ipop_bin_files; do
  cp ../bin/$file $version/ipop/bin/.
done

cp -axf ../drivers $version/ipop/.

cp -axf ../../brunet1/scripts $version/ipop/.
cp -axf ../config $version/ipop/.
cd ../docs
rm -rf doxy_out
doxygen ipop.doxy
cd -
mkdir $version/ipop/docs
cp -axf ../docs/doxy_out/* $version/ipop/docs

cd $version
zip -r9 ipop.src.$version.zip ipop.src
zip -r9 ipop.$version.zip ipop
cd -
mv $version/ipop.src.$version.zip .
mv $version/ipop.$version.zip .
rm -rf $version
