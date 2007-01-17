#define USE_HEIGHT
using System;

namespace Brunet.Coordinate {
  public class Point {
    protected static readonly int DIMENSIONS = 2;
    protected static readonly int INITIAL_VECTOR_VALUE  = 0;
    protected static readonly float MIN_HEIGHT = 0.01f;
    

    protected static readonly Random _rr = new Random();

    protected float[] _side;
    public float[] Side {
      get {
	return _side;
      } 
      set {
	_side = value;
      }
    }
    protected float _height;
    public float Height {
      get {
	return _height;
      }
      set {
	_height = value;
      }
    }
    
    public Point() {
      _side = new float[DIMENSIONS];
      for (int i = 0; i < _side.Length; i++) {
	_side[i] = INITIAL_VECTOR_VALUE;
      }
      _height = INITIAL_VECTOR_VALUE;
    }
    public Point GetDirection(Point p) {
      float dist = GetEucledianDistance(p);
      if (dist == 0) {
	return null;
      }
      Point unitVector = new Point();
      for (int i = 0; i < DIMENSIONS; i++) {
	unitVector.Side[i] = (p.Side[i] - _side[i])/dist;
      }
      unitVector.Height = (_height + p.Height)/dist; 
      return unitVector;
    }
    public float GetEucledianDistance(Point p) {
      return GetPlanarDistance(p) + _height + p.Height;
    }
    public float GetPlanarDistance(Point p) {
      float sum = 0;
      for (int i = 0; i < DIMENSIONS; i++) {
	sum += (float) Math.Pow(_side[i] - p.Side[i], 2);
      } 
      return (float) Math.Sqrt(sum);
    }
    public float Length() {
      float sum = 0.0f;
      for (int i = 0; i < DIMENSIONS; i++) {
	sum += (float) Math.Pow(_side[i], 2);
      }
      return (float) Math.Sqrt(sum + _height);
    }
    
    public void Bump() {
      for (int i = 0; i < DIMENSIONS; i++) {
	_side[i] = (float) _rr.NextDouble();
	Console.WriteLine("after bump side ({0}): {1}", i, _side[i]);
      }
#if USE_HEIGHT      
      //only then do we bump the height (not otherwise)
      _height = (float) _rr.NextDouble();
#endif
      Console.WriteLine("after bump height: {0}", _height);
    }
    
    public void Scale(float s) {
      for (int i = 0; i < DIMENSIONS; i++) {
	_side[i] = _side[i] * s;
      }
      _height = _height * s;
    }
    public void Add(Point p) {
      for (int i = 0; i < DIMENSIONS; i++) {
	_side[i] = _side[i] + p.Side[i];
      }
      _height = _height + p.Height;
    }
    public void Assign(Point p) {
      for (int i = 0; i < DIMENSIONS; i++) {
	_side[i]  = p.Side[i];
      }
      _height = p.Height;
    }
    public void CheckHeight() {
#if USE_HEIGHT
      while (_height < MIN_HEIGHT) {
	_height = (float) _rr.NextDouble();
      }
#endif
    }
    public static Point GetRandomUnitVector () {
      Point unitVector = new Point();
      float length = 0.0f;
      for (int i = 0; i < DIMENSIONS; i++) {
	unitVector.Side[i] = (float) _rr.NextDouble();
	length += unitVector.Side[i]*unitVector.Side[i];
      }  
      length = (float) Math.Sqrt(length);
      
      for (int i = 0; i < DIMENSIONS; i++) {
	unitVector.Side[i] /= length;
      }	
      unitVector.Height = 0.0f;
#if USE_HEIGHT
      unitVector.Height = (float) _rr.NextDouble();
#endif
      return unitVector;
    }
    public override string ToString() {
      string ss = "(( ";
      for (int i= 0; i < DIMENSIONS; i++) {
	ss += _side[i];
	if (i < DIMENSIONS - 1) {
	  ss += ", ";
	}
      }
      ss += ("), " + _height + ")");
      return ss;
    }
  }
}
