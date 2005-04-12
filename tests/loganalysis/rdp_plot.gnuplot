

#set xtics (0,500,1000,1500,2000,2500,3000)
#set ytics (0,1,5,10,15,20,25,30)
set ylabel "RDP"
set xlabel "PING Round Trip Time (milliseconds) (50 ms bins)"
set size 0.85
set yrange [0:20]
set term post eps color enhanced
set output "RDP_figure.eps"

plot 'test2.txt' using 1:3 title "Median" w l,\
1.0 notitle w l,\
'test2.txt' using 1:3:2:4  title "Minimum, Median, 90th Percentile" w yerrorbars
