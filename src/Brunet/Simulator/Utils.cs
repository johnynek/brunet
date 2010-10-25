using System;
using System.Collections.Generic;

namespace Brunet.Simulator {
  public class Utils {
    /// <summary>Calculates the average of a data set.</summary>
    public static double Average(List<int> data)
    {
      long total = 0;
      foreach(int point in data) {
        total += point;
      }

      return (double) total / data.Count;
    }

    /// <summary>Calculates the standard deviation given a data set and the
    /// average.</summary>
    public static double StandardDeviation(List<int> data, double avg)
    {
      double variance = 0;
      foreach(int point in data) {
        variance += Math.Pow(point - avg, 2.0);
      }

      return Math.Sqrt(variance / (data.Count - 1));
    }
  }
}
