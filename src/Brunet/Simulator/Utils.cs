using System;
using System.IO;
using System.Collections.Generic;
using Brunet.Collections;

namespace Brunet.Simulator {
  static public class Utils {
    public const int RANDOM_LATENCY_BOUND = 1048576;
    public const int RANDOM_LATENCY_DIVIDER = 1;

    /// <summary>Calculates the average of a data set.</summary>
    static public double Average(List<int> data)
    {
      long total = 0;
      foreach(int point in data) {
        total += point;
      }

      return (double) total / data.Count;
    }

    /// <summary>Calculates the standard deviation given a data set and the
    /// average.</summary>
    static public double StandardDeviation(List<int> data, double avg)
    {
      double variance = 0;
      foreach(int point in data) {
        variance += Math.Pow(point - avg, 2.0);
      }

      return Math.Sqrt(variance / (data.Count - 1));
    }

    /// <summary>Reads a space separated value file as latency data set.</summary>
    static public List<List<int>> ReadLatencyDataSet(string filename, int size)
    {
      var latency_map = new List<List<int>>();
      using(StreamReader fs = new StreamReader(new FileStream(filename, FileMode.Open))) {
        string line = null;
        while((line = fs.ReadLine()) != null) {
          string[] points = line.Split(' ');
          List<int> current = new List<int>(points.Length);
          foreach(string point in points) {
            int val;
            if(!Int32.TryParse(point, out val)) {
              val = 500000;
            } else if(val < 0) {
              val = 500000;
            }
            current.Add(val);
          }
          latency_map.Add(current);
        }
      }
      
      //If the size is less than the data set, we may get inconclusive
      // results as network size changes due to the table potentially being
      // heavy set early and lighter later.  This randomly orders all entries
      // so that multiple calls to the graph will provide a good distribution.
      if(size < latency_map.Count) {
        Random rand = Node.SimulatorRandom;
        Dictionary<int, int> chosen = new Dictionary<int, int>(size);
        for(int i = 0; i < size; i++) {
          int index = rand.Next(0, latency_map.Count - 1);
          while(chosen.ContainsKey(index)) {
            index = rand.Next(0, latency_map.Count - 1);
          }
          chosen.Add(i, index);
        }

        var new_latency_map = new List<List<int>>(latency_map.Count);
        for(int i = 0; i < size; i++) {
          List<int> map = new List<int>(size);
          for(int j = 0; j < size; j++) {
            map.Add(latency_map[chosen[i]][chosen[j]]);
          }
          new_latency_map.Add(map);
        }
        latency_map = new_latency_map;
      }
      return latency_map;
    }

    /// <summary>Generates a latency map using a 2-dimensional space.</summary>
    static public List<List<int>> RandomLatencyMap(int size)
    {
      var rand = Node.SimulatorRandom;
      var node_map = new List<Pair<int, int>>(size);
      for(int i = 0; i < size; i++) {
        int x = rand.Next(0, RANDOM_LATENCY_BOUND);
        int y = rand.Next(0, RANDOM_LATENCY_BOUND);
        node_map.Add(new Pair<int, int>(x, y));
      }

      var latency_map = new List<List<int>>(size);
      for(int i = 0; i < size; i++) {
        latency_map.Add(new List<int>(size));
        for(int j = 0; j < size; j++) {
          if(i == j) {
            latency_map[i].Add(0);
          } else {
            int distance = GeometricDistance(node_map[i], node_map[j]);
            latency_map[i].Add(distance / RANDOM_LATENCY_DIVIDER);
          }
        }
      }
      return latency_map;
    }

    /// <summary>Calculates the geometric distance for a "connected" plane.</summary>
    static public int GeometricDistance(Pair<int, int> p0, Pair<int, int> p1)
    {
      double x = Math.Abs(p0.First - p1.First);
      x = (x > RANDOM_LATENCY_BOUND / 2) ? x - RANDOM_LATENCY_BOUND : x;
      double y = Math.Abs(p0.Second - p1.Second);
      y = (y > RANDOM_LATENCY_BOUND / 2) ? y - RANDOM_LATENCY_BOUND : y;
      return (int) Math.Sqrt((x * x) + (y * y));
    }
  }
}
