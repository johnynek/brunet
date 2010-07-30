#!/bin/bash
version=$1
# Prep a clean repository
rm -rf $version
mkdir -p $version/acisp2p.src
git clone ../.. $version/tmp
cp -axf $version/tmp/* $version/acisp2p.src/.

# Create the source version
cd $version
zip -r9 ../acisp2p.src.$version.zip acisp2p.src
cd -

# Create the binary version
cd $version/acisp2p.src
nant
cd -

# Copy files to the binary directory
mkdir -p $version/acisp2p/bin
lib_files="Brunet.dll Brunet.Services.Dht.dll Brunet.Services.XmlRpc.dll Mono.Security.dll Brunet.Services.Coordinate.dll Brunet.Security.dll NDesk.Options.dll CookComputing.XmlRpcV2.dll Brunet.Xmpp.dll jabber-net.dll zlib.net.dll ManagedOpenSsl.dll"
bin_files="P2PNode.exe DhtIpopNode.exe GroupVPNService.exe"
for file in $lib_files; do
  cp $version/acisp2p.src/lib/$file $version/acisp2p/bin/.
done

for file in $bin_files; do
  cp $version/acisp2p.src/bin/$file $version/acisp2p/bin/.
done

cp groupvpn* $version/acisp2p/bin/.
cp daemon.py $version/acisp2p/bin/.
cp install* $version/acisp2p/.

mkdir $version/acisp2p/deb
cp deb/* $version/acisp2p/deb/.

cp -axf ../../drivers $version/acisp2p/.

scripts="bget.py bput.py crawl.py pybru.py"
for file in $scripts; do
  cp $version/acisp2p.src/scripts/$file $version/acisp2p/bin/.
done

cp -axf $version/acisp2p.src/config $version/acisp2p/.
cp $version/acisp2p.src/docs/release_notes.txt $version/acisp2p/.
cp $version/acisp2p.src/README $version/acisp2p/.
echo $version > $version/acisp2p/version

# Store the binary version
cd $version
zip -r9 ../acisp2p.$version.zip acisp2p
# All done
cd -
rm -rf $version
