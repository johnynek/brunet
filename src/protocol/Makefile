all : Makefile.tmp
	make -f Makefile.tmp all
# This target makes the temporary Makefile which
# is produced by our "hack"-automake perl script:
Makefile.tmp : mkdep.pl *.cs
	./mkdep.pl > Makefile.tmp
%.dll : Makefile.tmp
	make -f Makefile.tmp $@
%.exe : Makefile.tmp
	make -f Makefile.tmp $@
clean : Makefile.tmp
	make -f Makefile.tmp clean
	rm Makefile.tmp
cleanlog :
	rm -f BootGraph_*[^cs]
	rm -f BootGraph_*ps
	rm -f boot.log boot.out
# DO NOT DELETE
