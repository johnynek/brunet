/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2008-2010 Taewoong Choi <twchoi@ufl.edu> University of Florida  
Copyright (C) 2010 P. Oscar Boykin <boykin@pobox.com> University of Florida  

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;

using Brunet.Services.MapReduce; 
using Brunet.Messaging;
using Brunet.Concurrent;
using Brunet.Collections;
using Brunet.Util;
namespace Brunet.Services.Deetoo {
  /**
   * This class implements a map-reduce task that allows regular 
   * expression search using Bounded Broadcasting. 
   */ 
  public class MapReduceQuery: MapReduceBoundedBroadcast {
    private readonly CacheList _cl;
    private readonly Mutable<MRQState> _mut; 
    private class MRQState {
      public readonly ImmutableHashtable<string, Converter<object,QueryMatcher>> QMFact;
      public readonly ImmutableHashtable<string, Converter<object,HitCombiner>> HCFact;
      public MRQState() {
        QMFact = ImmutableHashtable<string, Converter<object,QueryMatcher>>.Empty;
        HCFact = ImmutableHashtable<string, Converter<object,HitCombiner>>.Empty;
      }
      private MRQState(ImmutableHashtable<string, Converter<object,QueryMatcher>> qm,
                       ImmutableHashtable<string, Converter<object,HitCombiner>> hc) {
        QMFact = qm;
        HCFact = hc;
      }
      public MRQState AddQM(string type, Converter<object,QueryMatcher> fact) {
        return new MRQState(QMFact.InsertIntoNew(type, fact), HCFact);
      }
      public MRQState AddHC(string type, Converter<object,HitCombiner> fact) {
        return new MRQState(QMFact, HCFact.InsertIntoNew(type, fact));
      }
    }
    public MapReduceQuery(Node n, CacheList cl): base(n) {
      _cl = cl;
      _mut = new Mutable<MRQState>(new MRQState());
      //Add the default query handlers:
      AddQueryMatcher("regex", delegate(object pattern) {
                                 return new RegexMatcher((string)pattern);
                               });
      AddQueryMatcher("exact", delegate(object pattern) { return new ExactMatcher(pattern); });
      AddHitCombiner("concat", delegate(object arg) { return ConcatCombiner.Instance; });
      AddHitCombiner("maxcount", delegate(object arg) { return new MaxCountCombiner((int)arg); });
    }
    public void AddQueryMatcher(string type, Converter<object,QueryMatcher> qmf) {
      _mut.Update(delegate(MRQState old) { return old.AddQM(type, qmf); });
    }
    public void AddHitCombiner(string type, Converter<object,HitCombiner> hcf) {
      _mut.Update(delegate(MRQState old) { return old.AddHC(type, hcf); });
    }
    /*
     * Map method to add CachEntry to CacheList
     * @param map_arg [pattern, query_type]
     */
    public override void Map(Channel q, object map_arg) {
      IList map_args = (IList)map_arg;
      string query_type = (string)(map_args[0]);
      Converter<object,QueryMatcher> qmf = null;
      if( _mut.State.QMFact.TryGetValue(query_type, out qmf) ) {
        QueryMatcher qm = qmf(map_args[1]);
        var my_entry = new ListDictionary();
        var results = new ArrayList();
        foreach(Entry e in _cl.MState.State.Data) {
          object cont = e.Content;
          if(qm.Match(cont)) {
            results.Add(cont);
          }
        }
        my_entry["query_result"] = results;
        my_entry["count"] = 1;
        my_entry["height"] = 1;
        q.Enqueue(my_entry);
      }
      else {
        q.Enqueue(new AdrException(-32608, "No Deetoo match option with this name: " +  query_type) );
        return;
      }
    }
      
    /**
     * Reduce method
     * @param reduce_args argument for reduce
     * @param current_result result of current map 
     * @param child_rpc results from children 
     * return table of hop count, tree depth, and query result
     */  
    public override void Reduce(Channel q, object reduce_args, 
                                  object current_result, RpcResult child_rpc) {
      object child_result = null;
      child_result = child_rpc.Result;
      Converter<object,HitCombiner> hcf = null;
      IList args = (IList)reduce_args;
      string query_type = (string)args[0];
      object hc_arg = args[1];
      if( _mut.State.HCFact.TryGetValue(query_type, out hcf) ) {
        HitCombiner hc = hcf(hc_arg);
        if( current_result == null ) {
          IDictionary child_val = (IDictionary)child_result;
          //Initially, we are empty:
          var res = hc.Combine(new ArrayList(), (IList)child_val["query_result"]);
          var ret_val = new ListDictionary();
          ret_val["height"] = child_val["height"];
          ret_val["count"] = child_val["count"];
          ret_val["query_result"] = res.First;
          q.Enqueue(new Brunet.Collections.Pair<object, bool>(ret_val, res.Second));
        }
        else {
          IDictionary current_val = (IDictionary)current_result;
          IDictionary child_val = (IDictionary)child_result;
          
          var ret_val = new ListDictionary();
          ret_val["count"] = (int)current_val["count"] + (int)child_val["count"];
          ret_val["height"] = Math.Max((int)current_val["height"],
                                      1 + (int)child_val["height"]);
          
          var res = hc.Combine((IList)current_val["query_result"],
                                 (IList)child_val["query_result"]);
          ret_val["query_result"] = res.First;
          q.Enqueue(new Brunet.Collections.Pair<object, bool>(ret_val, res.Second));
        }
      }
      else {
        q.Enqueue(new AdrException(-32608, "This query type {0} is not supported." + query_type) );
      }
    }
  }
}
