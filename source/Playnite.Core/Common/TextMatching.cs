using System;
using System.Globalization;
using System.Linq;
using Playnite.SDK;

namespace Playnite.Common
{
    // Fuzzy text matching used by search and name filtering. Lives in the core
    // so every host (WPF, Avalonia, tests) filters identically by default;
    // formerly a static on the WPF-side SearchViewModel.
    public static class TextMatching
    {
        public const double DefaultMinimumJaronWinklerSimilarity = 0.90;
        private static readonly char[] textMatchSplitter = new char[] { ' ' };

        public static bool MatchTextFilter(string filter, string toMatch, bool matchTargetAcronymStart, double minimumJaronWinklerSimilarity = DefaultMinimumJaronWinklerSimilarity)
        {
            if (filter.IsNullOrWhiteSpace())
            {
                return true;
            }

            if (!filter.IsNullOrWhiteSpace() && toMatch.IsNullOrWhiteSpace())
            {
                return false;
            }

            if (filter.IsNullOrWhiteSpace() && toMatch.IsNullOrWhiteSpace())
            {
                return true;
            }

            if (filter.GetJaroWinklerSimilarityIgnoreCase(toMatch) >= minimumJaronWinklerSimilarity)
            {
                return true;
            }

            if (filter.Length > toMatch.Length)
            {
                return false;
            }

            if (matchTargetAcronymStart && filter.IsStartOfStringAcronym(toMatch))
            {
                return true;
            }

            var filterSplit = filter.Split(textMatchSplitter, StringSplitOptions.RemoveEmptyEntries);
            var toMatchSplit = toMatch.Split(textMatchSplitter, StringSplitOptions.RemoveEmptyEntries);
            var allMatch = true;
            // This is pretty crude, but it works for most cases and provides relatively good results.
            // TODO definitely could use some improvements for better fuzzy results.
            foreach (var word in filterSplit)
            {
                if (!toMatchSplit.Any(a => a.ContainsInvariantCulture(word, CompareOptions.IgnoreCase | CompareOptions.IgnoreSymbols | CompareOptions.IgnoreNonSpace)))
                {
                    allMatch = false;
                    break;
                }
            }

            return allMatch;
        }
    }
}
