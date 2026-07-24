using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Utility for matching items in a collection where each query is likely to relate to the previous query.
/// </summary>
/// <typeparam name="T">Type of each item in the collection</typeparam>
/// <typeparam name="U">Type of the query</typeparam>
/// <param name="values">Values to query</param>
/// <param name="queryComparer">A comparer to determine if one query is more specific than another. For string queries, this is a comparison of the length. </param>
/// <param name="matcher">Given query U, does item T match?</param>
public partial class SimplePersistentMatcher<T, U>(IReadOnlyCollection<T> values, Comparison<U> queryComparer, Func<T, U, bool> matcher)
{
    private HashSet<T> filteredSet = [..values];
    private U lastQuery;
    private readonly IReadOnlyCollection<T> values = values;
    private readonly Comparison<U> queryComparer = queryComparer;
    private readonly Func<T, U, bool> matcher = matcher;

    /// <summary>
    /// Note that if an item matches, it is possible that removed(item) and added(item) can be called (in that order)
    /// </summary>
    /// <param name="query"></param>
    /// <param name="added"></param>
    /// <param name="removed"></param>
    public void Filter(U query, Action<T> added, Action<T> removed)
    {
        if (queryComparer(query, lastQuery) < 0)
        {
            foreach (var item in filteredSet)
            {
                removed(item);
            }
            filteredSet.Clear();

            foreach (var item in values)
            {
                if (matcher(item, query))
                {
                    filteredSet.Add(item);
                    added(item);
                }
            }
        } 
        else
        {
            foreach (var item in filteredSet)
            {
                if (matcher(item, query))
                {
                    continue;
                }
                filteredSet.Remove(item);
                removed(item);
            }
        }

        lastQuery = query;
    }

    public void Add(T item)
    {
        if (lastQuery != null && matcher(item, lastQuery))
        {
            filteredSet.Add(item);
        }
    }
}
