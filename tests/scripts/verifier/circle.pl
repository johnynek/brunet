#!/usr/bin/perl

use Shell;
use Getopt::Long;

# read options
$result = GetOptions ("log=s" => \$logfile);  

$tmpfile = $logfile . ".tmp";
$outfile = $logfile . "out1.ps";

$cmd1 = "./bgraph.py";
$cmd2 = "./ringo1.py";

`$cmd1  $logfile |sort -g |grep 'blue' >  $tmpfile`;

`$cmd2 $tmpfile`;

$tmpfile2 = $tmpfile . "circle";

$cmd3 = "neato -Tps -n -s72";
`$cmd3  $tmpfile2 -o $outfile`;

`rm -f $tmpfile`;
`rm -f $tmpfile2`;

use Shell;
use Getopt::Long;

# read options
$result = GetOptions ("log=s" => \$logfile);  

$tmpfile = $logfile . ".tmp";
$outfile = $logfile . "out2.ps";

$cmd1 = "./bgraph.py";
$cmd2 = "./ringo2.py";

`$cmd1  $logfile |sort -g |grep 'blue' >  $tmpfile`;

`$cmd2 $tmpfile`;

$tmpfile2 = $tmpfile . "circle";

$cmd3 = "neato -Tps -n -s72";
`$cmd3  $tmpfile2 -o $outfile`;

#`rm -f $tmpfile`;
#`rm -f $tmpfile2`;
