using System.Collections;
using Brunet.Collections;

namespace Brunet.Services.Deetoo {

public abstract class HitCombiner {

  /** combine a list of hits, signal when done
   * @arg oldh the hits before adding the new hits (initially empty)
   * @arg newh the recently found hits.
   * @return pair of the combined list and true if we are done.
   */
  public abstract Pair<IList, bool> Combine(IList oldh, IList newh);
  
  private class NullComparer : System.Collections.IComparer {
    public static readonly NullComparer Instance = new NullComparer();
    private NullComparer() { }
    public int Compare(object o1, object o2) {
      return 0;
    }
  }
  
  /** Implement this if you want to sort your results
   */ 
  public virtual System.Collections.IComparer GetComparer() {
    return NullComparer.Instance; 
  }

}

public class ConcatCombiner : HitCombiner {
  public readonly static ConcatCombiner Instance = new ConcatCombiner();
  protected ConcatCombiner() {
  
  }

  public override Pair<IList, bool> Combine(IList oldh, IList newh) {
    ArrayList result = new ArrayList();
    result.AddRange(oldh);
    foreach(object newit in newh) {
      if(!result.Contains(newit)) {
        result.Add(newit);
      }
    }
    result.Sort(GetComparer());
    return new Pair<IList,bool>(result, false);
  }
}

public class MaxCountCombiner : HitCombiner {
  private readonly int Max;
  public MaxCountCombiner(int max) {
    Max = max;
  }
  public override Pair<IList, bool> Combine(IList oldh, IList newh) {
    ArrayList result = new ArrayList();
    result.AddRange(oldh);
    foreach(object newit in newh) {
      if(!result.Contains(newit)) {
        result.Add(newit);
      }
    }
    result.Sort(GetComparer());
    bool done = result.Count >= Max;
    if( done ) {
      result.RemoveRange(Max, result.Count - Max);
    }
    return new Pair<IList,bool>(result, done);
  }
}

}
