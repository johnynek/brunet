#!/usr/bin/perl

open(FILES, "ls *.cs|");

while(<FILES>) {
  chomp;
  $file = $_;
  open(SRC, "<$file") or die "Can't open $file\n";
  if( $file =~/(.*)\.cs$/ ) {
    $base = $1;
    $targets{$file} = $base;
  }
  else {
    next;
  }
  $isexe{$base} = 0;
  while(<SRC>) {
    
    #if( /Brunet\.([^;\s\(\)\.\,]+)/ ) {
    if( /Brunet\.([\w\d]+)/ ) {
        #Use a hash so dependencies are not counted twice
        $depgraph{$file}{$1} = 1;
        #print "$file : $1\n";
    }
    else {
      #print;
      #print "no match";
    }
    if( /static(.*)Main/ ) {
       $isexe{$base} = 1;
    }
  }
}

#we know the dependencies, now print out the make file:

#here is the make all target:

#foreach $file (keys %targets) {
#$base = $targets{$file};
while( ($file, $base) = each(%targets) ) {
  $alldeps .= $base . '.dll ';
  $alldlls .= $base . '.dll ';
  #Make also the exe if it has a Main function:
  if( $isexe{$base} == 1 ) {
    $alldeps .= $base . '.exe ';
    $exedlls .= $base . '.dll,'; #for making the gnucla.dll
  }
  else { #for making the gnucla.dll
    $justdlls .= $base . '.dll ';
    $dllsrc .= $file . ' ';
  }
}
print "all : $alldeps\n\n";

#Here is the clean target:
print "clean :\n\t/bin/rm -f $alldeps Brunet.dll\n\n";

#Here is the gnucla.dll target:
chop($exedlls);
print "Brunet.dll : $alldlls\n" .
      #"\tmcs /target:library $dllsrc -r:$exedlls,log4net.dll -lib:./ /out:gnucla.dll\n\n";
      "\tmcs /target:library $dllsrc -lib:../../lib/ -r:log4net.dll,$exedlls /out:Brunet.dll\n\n";

#Now we make the other targets:
foreach $file (keys %targets) {

  $base = $targets{$file};
  if( $isexe{ $base } == 1 ) {
    #Then make an extra target for the dll that only depends on the exe:
    print "$base.dll : $base.exe\n\n";
    $suffix = '.exe';
  }
  else {
    $suffix = '.dll';
  }

  print $base.$suffix. ' : ';
  $files = $file . ' ';
  $modules = "";
  foreach $dep (keys %{$depgraph{$file}})
  {
    $dep .= '.dll';
    $files .= $dep . ' ';
    if( $modules ne "" ) { $modules .= ","; }
    $modules .= $dep;
  }
  print $files;
  if( $isexe{$base} == 0 ) {
    #print "\n\tmcs /t:library $file -o " . $targets{$file} . ".dll -lib:./";
    print "\n\tmcs /t:library $file -o " . $targets{$file} . ".dll -lib:../../lib/";
    if( $modules ne "") { print " -r:log4net.dll,$modules"; }
  }
  else {
    #For exe's we also compile it as a dll:
    #print "\n\tmcs /t:library $file -o " . $targets{$file} . ".dll -lib:./";
    print "\n\tmcs /t:library $file -o " . $targets{$file} . ".dll -lib:../../lib/";
    if( $modules ne "") { print " -r:log4net.dll,$modules"; }
    #print "\n\tmcs $file -o " . $targets{$file} . ".exe -lib:./";
    print "\n\tmcs $file -o " . $targets{$file} . ".exe -lib:../../lib/";
    if( $modules ne "") { print " -r:log4net.dll,$modules"; }
  }
  
  print "\n\n"; 
}
