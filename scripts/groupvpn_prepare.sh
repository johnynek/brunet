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

mv $TMPDIR/webcert $CERTSDIR/.
mv $TMPDIR/cacert $CERTSDIR/.

ETCDIR=$DIR/etc
mv $TMPDIR/*config $ETCDIR/.
SEDDIR=`echo $ETCDIR | sed -e 's/\//\\\\\//g'`
sed -i s/\<KeyPath\>/\<KeyPath\>$SEDDIR\\// $ETCDIR/node.config
sed -i s/\<CertificatePath\>certificates/\<CertificatePath\>$SEDDIR\\/certificates/ $ETCDIR/node.config

rm -rf $TMPDIR

echo "Done setting up GroupVPN configuration."
