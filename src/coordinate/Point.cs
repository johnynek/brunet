//#define USE_HEIGHT
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
#if USE_HEIGHT
      _height = INITIAL_VECTOR_VALUE;
#endif
    }
    
    public Point(float[] side, float height) {
      if (side.Length != DIMENSIONS) {
	throw new Exception("Only 2-D points supported");
      }
      _height = height;
      _side = new float[DIMENSIONS];
      for (int i = 0; i < DIMENSIONS; i++) {
	_side[i] = side[i];
      }
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
#if USE_HEIGHT
      unitVector.Height = (_height + p.Height)/dist; 
#endif
      return unitVector;
    }
    public float GetEucledianDistance(Point p) {
      float d =  GetPlanarDistance(p);
#if USE_HEIGHT      
      d = d + _height + p.Height;
#endif
      return d;
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
      float d = (float) Math.Sqrt(sum);
#if USE_HEIGHT
      d = d + _height;
#endif
      return d;
    }
    
    public void Bump() {
      for (int i = 0; i < DIMENSIONS; i++) {
	_side[i] = (float) _rr.NextDouble();
	Console.WriteLine("after bump side ({0}): {1}", i, _side[i]);
      }
#if USE_HEIGHT      
      //only then do we bump the height (not otherwise)
      _height = (float) _rr.NextDouble();
      Console.WriteLine("after bump height: {0}", _height);
#endif
    }
    
    public void Scale(float s) {
      for (int i = 0; i < DIMENSIONS; i++) {
	_side[i] = _side[i] * s;
      }
#if USE_HEIGHT
      _height = _height * s;
#endif
    }

    public void Add(Point p) {
      for (int i = 0; i < DIMENSIONS; i++) {
	_side[i] = _side[i] + p.Side[i];
      }
#if USE_HEIGHT
      _height = _height + p.Height;
#endif
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
#if USE_HEIGHT
      unitVector.Height = 0.0f;
      unitVector.Height = (float) _rr.NextDouble();
#endif
      return unitVector;
    }
    public override string ToString() {
      string ss = "( ";
      for (int i= 0; i < DIMENSIONS; i++) {
	ss += _side[i];
	if (i < DIMENSIONS - 1) {
	  ss += ", ";
	}
      }
#if USE_HEIGHT
      ss +=  ("h " + _height + ")");
#else
      ss += ")";
#endif
      return ss;
    }

    public bool Equals(Point other) {
      if (other.Side.Length != Side.Length) {
	return false;
      }

      for (int i = 0; i < DIMENSIONS; i++) {
	if (other.Side[i] - Side[i] > 0.1 ||
	    other.Side[i] - Side[i] < -0.1) {
	  return false;
	}
      }
#if USE_HEIGHT      
      if (other.Height - Height > 0.1 ||
	  other.Height - Height < -0.1) {
	return false;
      }
#endif
      return true;
    }
  }
}
