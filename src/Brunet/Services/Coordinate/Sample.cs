using System;
using System.Collections;


using Brunet;

namespace Brunet.Services.Coordinate {
  /** Keeps track of latency samples received from a neighbor. */
  public class Sample {
    protected static readonly int PING_HISTORY_COUNT = 4;
    protected static readonly int MIN_HISTORY_COUNT = 0;
    protected static readonly double PING_SAMPLE_PERCENTILE = 0.25f;

    /** weighted error last reposrted by a neighbor. */
    protected double _weightedError;
    public double WeightedError {
      get {
	return _weightedError;
      }
    }

    /** last reported coordinates. */
    protected Point _position;
    public Point Position {
      get {
	return _position;
      }
    }
    
    /**list of latency samples. */
    protected ArrayList _sample_list;
    
    /** timestamp.*/
    protected DateTime _time_stamp;
    public DateTime TimeStamp {
      get {
	return _time_stamp;
      }
    }
    public Sample() {
      _sample_list = new ArrayList();
    }

    public void AddSample(DateTime time_stamp, double sample, Point position, double weightedError) {
      _time_stamp = time_stamp;
      _weightedError = weightedError;
      _position = position;
      if (_sample_list.Count > PING_HISTORY_COUNT) {
	_sample_list.RemoveAt(0);
      }

      _sample_list.Add(sample);
    }

    public double GetSample() {
      if (_sample_list.Count > MIN_HISTORY_COUNT) {
	ArrayList sorted_samples = new ArrayList(_sample_list);
	sorted_samples.Sort();
	int percentile = (int) (PING_SAMPLE_PERCENTILE * _sample_list.Count);
	double sample = (double) sorted_samples[percentile];
	return sample;
      } else {
	return -1.0f;
      }
    }
  }
}
