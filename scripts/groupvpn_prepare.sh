#!/bin/bash
source /etc/ipop.vpn.config

ZIPFILE=$1
if [[ ! "$ZIPFILE" ]]; then
  echo "Please specify a path to a zip file containing GroupVPN configuration information."
fi

CERTSDIR=$DIR/etc/certificates
rm -rf $CERTSDIR &> /dev/null
mkdir -p $CERTSDIR &> /dev/null

TMPDIR=$DIR/$RANDOM
mkdir $TMPDIR
unzip $ZIPFILE -d $TMPDIR &> /dev/null
if [[ $? != 0 ]]; then
  exit 1
fi

for cert in webcert cacert; do
  if test -e $TMPDIR/$cert; then
    mv $TMPDIR/$cert $CERTSDIR/.
  else
    echo "Missing $cert, this doesn't imply failure, this may be an insecure groupvpn config"
  fi
done

ETCDIR=$DIR/etc
for config in node.config ipop.config bootstrap.config dhcp.config; do
  if test -e $TMPDIR/$config; then
    mv $TMPDIR/$config $ETCDIR/.
  elif [[ $config != "bootstrap.config" ]]; then
    echo "Missing $config, configuration setup failed!"
    exit 1
  fi
done

SEDDIR=`echo $ETCDIR | sed -e 's/\//\\\\\//g'`
sed -i s/\<KeyPath\>/\<KeyPath\>$SEDDIR\\// $ETCDIR/node.config
sed -i s/\<CertificatePath\>certificates/\<CertificatePath\>$SEDDIR\\/certificates/ $ETCDIR/node.config

rm -rf $TMPDIR

echo "Done setting up GroupVPN configuration."
