using System;
using System.Collections;


using Brunet;

namespace Brunet.Coordinate {
  /** Keeps track of latency samples received from a neighbor. */
  public class Sample {
    protected static readonly int PING_HISTORY_COUNT = 4;
    protected static readonly float PING_SAMPLE_PERCENTILE = 0.25f;

    /** weighted error last reposrted by a neighbor. */
    protected float _weightedError;
    public float WeightedError {
      get {
	return _weightedError;
      }
    }

    /** last resported coordinates. */
    protected Point _position;
    public Point Position {
      get {
	return _position;
      }
    }
    
    /**list of sameples. */
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
    public void AddSample(DateTime time_stamp, float sample, Point position, float weightedError) {
      _time_stamp = time_stamp;
      _weightedError = weightedError;
      _position = position;

      if (_sample_list.Count > PING_HISTORY_COUNT) {
	_sample_list.RemoveAt(0);
      }
      _sample_list.Add(sample);
    }
    public float GetSample() {
      if (_sample_list.Count < PING_HISTORY_COUNT/2) {
	//we just dont start assuming samples to be correct; unless we have many of those
	return -1;
      }
      ArrayList sorted_samples = new ArrayList(_sample_list);
      sorted_samples.Sort();
      int percentile = (int) (PING_SAMPLE_PERCENTILE * _sample_list.Count);
      float sample = (float) sorted_samples[percentile];
      return sample;
    }
  }

}
