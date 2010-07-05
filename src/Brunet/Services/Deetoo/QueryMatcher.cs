using System.Text.RegularExpressions;

namespace Brunet.Services.Deetoo {

  public abstract class QueryMatcher {
    /** returns true if this object matches the current query
     */
    public abstract bool Match(object data);
  }

  public class RegexMatcher : QueryMatcher {
    private readonly Regex RE;
    public RegexMatcher(string pattern) {
      RE = new Regex(pattern);
    }
    public override bool Match(object data) {
      return RE.IsMatch(data.ToString());
    }
  }
  public class ExactMatcher : QueryMatcher {
    private readonly object Value;
    public ExactMatcher(object val) {
      Value = val;
    }
    public override bool Match(object data) {
      return Value != null ? Value.Equals(data) : data == null;
    }
  }
}
