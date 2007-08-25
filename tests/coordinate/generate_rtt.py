#!/usr/bin/python

import sys
import random
import math

rtt = {}
num_sites = int(sys.argv[1])
num_hosts = int(sys.argv[2])

sites = {}
points = []
#generate sites over a plane of 1000 x 1000
for i in range(0, num_sites):
    x = random.random()*500
    y = random.random()*500
    sites[i] = (x,y)

#now generate points centered around the cites with a gaussian distribution
for i in range(0, num_hosts*num_sites):
    i_site = i/num_hosts
    (mu_x,mu_y) = sites[i_site]
    x = random.gauss(mu_x, 5)
    y = random.gauss(mu_y, 5)
    points.append((x, y))

#now generate the latency matrix for points
for i in range(0, num_hosts*num_sites):
    for j in range(i + 1, num_hosts*num_sites):
        (xi, yi) = points[i]
        (xj, yj) = points[j]        
        rtt[(i,j)] = math.sqrt((xi - xj)**2 + (yi - yj)**2)
        print i, j, rtt[(i, j)]

        
