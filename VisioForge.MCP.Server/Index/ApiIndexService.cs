using System.Text.Json;

namespace VisioForge.MCP.Server.Index;

/// <summary>
/// Singleton service that loads the API index and provides lookup/search capabilities.
/// Registered as a singleton in DI; loaded at startup.
/// </summary>
public class ApiIndexService
{
    private ApiIndexData _data = new();

    // Lookup dictionaries for fast access
    private Dictionary<string, ClassInfo> _classesByName = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ClassInfo> _classesByFullName = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, EnumInfo> _enumsByName = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, EnumInfo> _enumsByFullName = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<ClassInfo>> _classesByNamespace = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<ClassInfo>> _mediaBlocksByCategory = new(StringComparer.OrdinalIgnoreCase);

    // Inverted index for full-text search
    private Dictionary<string, HashSet<string>> _searchIndex = new(StringComparer.OrdinalIgnoreCase);

    // Sorted term list for efficient prefix matching (binary search instead of full scan)
    private string[] _sortedTerms = [];

    public ApiIndexData Data => _data;
    public string SdkVersion => _data.SdkVersion;
    public string GeneratedAt => _data.GeneratedAt;

    public void Load(string jsonPath)
    {
        Console.WriteLine($"Loading API index from: {jsonPath}");
        var json = File.ReadAllText(jsonPath);
        _data = JsonSerializer.Deserialize<ApiIndexData>(json)
            ?? throw new InvalidOperationException("Failed to deserialize API index");

        BuildLookups();
        BuildSearchIndex();

        Console.WriteLine($"API index loaded: {_data.Classes.Count} classes, {_data.Enums.Count} enums");
    }

    private void BuildLookups()
    {
        _classesByName.Clear();
        _classesByFullName.Clear();
        _enumsByName.Clear();
        _enumsByFullName.Clear();
        _classesByNamespace.Clear();
        _mediaBlocksByCategory.Clear();

        foreach (var cls in _data.Classes)
        {
            // Use the most specific (last) entry for duplicate names
            _classesByName[cls.Name] = cls;
            _classesByFullName[cls.FullName] = cls;

            if (!_classesByNamespace.ContainsKey(cls.Namespace))
                _classesByNamespace[cls.Namespace] = [];
            _classesByNamespace[cls.Namespace].Add(cls);

            if (cls.IsMediaBlock && cls.MediaBlockCategory != null)
            {
                if (!_mediaBlocksByCategory.ContainsKey(cls.MediaBlockCategory))
                    _mediaBlocksByCategory[cls.MediaBlockCategory] = [];
                _mediaBlocksByCategory[cls.MediaBlockCategory].Add(cls);
            }
        }

        foreach (var e in _data.Enums)
        {
            _enumsByName[e.Name] = e;
            _enumsByFullName[e.FullName] = e;
        }
    }

    private void BuildSearchIndex()
    {
        _searchIndex.Clear();

        foreach (var cls in _data.Classes)
        {
            var id = cls.FullName;
            IndexTerms(id, cls.Name);
            IndexTerms(id, cls.Summary);
            IndexTerms(id, cls.Remarks);
            if (cls.MediaBlockCategory != null)
                IndexTerms(id, cls.MediaBlockCategory);
            if (cls.SdkModule != null)
                IndexTerms(id, cls.SdkModule);

            foreach (var prop in cls.Properties)
            {
                IndexTerms(id, prop.Name);
                IndexTerms(id, prop.Summary);
            }

            foreach (var method in cls.Methods)
            {
                IndexTerms(id, method.Name);
                IndexTerms(id, method.Summary);
            }

            foreach (var evt in cls.Events)
            {
                IndexTerms(id, evt.Name);
                IndexTerms(id, evt.Summary);
            }
        }

        foreach (var e in _data.Enums)
        {
            var id = $"enum:{e.FullName}";
            IndexTerms(id, e.Name);
            IndexTerms(id, e.Summary);
            foreach (var v in e.Values)
            {
                IndexTerms(id, v.Name);
                IndexTerms(id, v.Summary);
            }
        }

        // Build sorted term array for efficient prefix search via binary search
        _sortedTerms = _searchIndex.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private void IndexTerms(string documentId, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // Tokenize: split by non-alphanumeric, also split CamelCase
        var words = Tokenize(text);

        foreach (var word in words)
        {
            if (word.Length < 2) continue;

            if (!_searchIndex.ContainsKey(word))
                _searchIndex[word] = [];
            _searchIndex[word].Add(documentId);
        }
    }

    // Reusable delimiter array to avoid allocation on every Tokenize() call
    private static readonly char[] TokenDelimiters = [' ', '.', ',', '(', ')', '<', '>', '{', '}', '[', ']', ':', ';', '/', '\\', '-', '_', '"', '\''];

    private static IEnumerable<string> Tokenize(string text)
    {
        // Split on non-alphanumeric using static delimiter array
        var parts = text.Split(TokenDelimiters, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            yield return part.ToLowerInvariant();

            // Also split CamelCase words
            var camelParts = SplitCamelCase(part);
            foreach (var cp in camelParts)
            {
                if (cp.Length >= 2)
                    yield return cp.ToLowerInvariant();
            }
        }
    }

    private static List<string> SplitCamelCase(string input)
    {
        var result = new List<string>();
        int start = 0;

        for (int i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]) && !char.IsUpper(input[i - 1]))
            {
                result.Add(input[start..i]);
                start = i;
            }
        }

        result.Add(input[start..]);
        return result;
    }

    // === Lookup Methods ===

    public ClassInfo? GetClassByName(string name)
    {
        _classesByName.TryGetValue(name, out var cls);
        return cls;
    }

    public ClassInfo? GetClassByFullName(string fullName)
    {
        _classesByFullName.TryGetValue(fullName, out var cls);
        return cls;
    }

    public EnumInfo? GetEnumByName(string name)
    {
        _enumsByName.TryGetValue(name, out var e);
        return e;
    }

    public List<ClassInfo> GetMediaBlocks(string? category = null)
    {
        if (category != null)
        {
            return _mediaBlocksByCategory.TryGetValue(category, out var blocks)
                ? blocks
                : [];
        }

        return _data.Classes.Where(c => c.IsMediaBlock).ToList();
    }

    public List<ClassInfo> GetCoreSdkClasses()
    {
        return _data.Classes.Where(c => c.IsCoreSDK).ToList();
    }

    public List<ClassInfo> GetClassesByNamespace(string ns)
    {
        return _classesByNamespace.TryGetValue(ns, out var classes) ? classes : [];
    }

    public List<string> GetChildNamespaces(string parentNs)
    {
        var prefix = string.IsNullOrEmpty(parentNs) ? "" : parentNs + ".";
        return _data.Namespaces
            .Where(n => n.StartsWith(prefix) && n.Length > prefix.Length)
            .Select(n =>
            {
                var rest = n[prefix.Length..];
                var dotIdx = rest.IndexOf('.');
                return dotIdx >= 0 ? prefix + rest[..dotIdx] : n;
            })
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }

    // === Search ===

    /// <summary>
    /// Find all indexed terms that start with the given prefix using binary search.
    /// O(log N) to find the start position, then linear scan over matching terms only.
    /// </summary>
    private IEnumerable<string> FindTermsWithPrefix(string prefix)
    {
        if (_sortedTerms.Length == 0) yield break;

        // Binary search for the first term >= prefix
        int lo = 0, hi = _sortedTerms.Length - 1;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (string.Compare(_sortedTerms[mid], prefix, StringComparison.OrdinalIgnoreCase) < 0)
                lo = mid + 1;
            else
                hi = mid;
        }

        // Walk forward while terms still match the prefix
        for (int i = lo; i < _sortedTerms.Length; i++)
        {
            if (_sortedTerms[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                yield return _sortedTerms[i];
            else
                break;
        }
    }

    public List<SearchResult> Search(string query, int maxResults = 20)
    {
        var queryTerms = Tokenize(query).Distinct().ToList();
        if (queryTerms.Count == 0) return [];

        // Count how many query terms match each document
        var scores = new Dictionary<string, int>();

        foreach (var term in queryTerms)
        {
            // Exact match
            if (_searchIndex.TryGetValue(term, out var docs))
            {
                foreach (var doc in docs)
                {
                    scores.TryGetValue(doc, out var score);
                    scores[doc] = score + 3; // Exact match gets higher score
                }
            }

            // Prefix match via binary search on sorted term array (O(log N) to find start)
            var prefixMatches = FindTermsWithPrefix(term);
            foreach (var matchedTerm in prefixMatches)
            {
                if (matchedTerm.Equals(term, StringComparison.OrdinalIgnoreCase)) continue;

                if (_searchIndex.TryGetValue(matchedTerm, out var prefixDocs))
                {
                    foreach (var doc in prefixDocs)
                    {
                        scores.TryGetValue(doc, out var score);
                        scores[doc] = score + 1;
                    }
                }
            }
        }

        // Build results
        var results = new List<SearchResult>();

        foreach (var (docId, score) in scores.OrderByDescending(kv => kv.Value).Take(maxResults))
        {
            if (docId.StartsWith("enum:"))
            {
                var enumFullName = docId[5..];
                var enumInfo = _enumsByFullName.GetValueOrDefault(enumFullName);
                if (enumInfo != null)
                {
                    results.Add(new SearchResult
                    {
                        Name = enumInfo.Name,
                        FullName = enumInfo.FullName,
                        Kind = "enum",
                        Summary = enumInfo.Summary,
                        Score = score,
                    });
                }
            }
            else
            {
                var classInfo = _classesByFullName.GetValueOrDefault(docId);
                if (classInfo != null)
                {
                    results.Add(new SearchResult
                    {
                        Name = classInfo.Name,
                        FullName = classInfo.FullName,
                        Kind = classInfo.IsMediaBlock ? "media_block" : (classInfo.IsCoreSDK ? "core_sdk" : classInfo.Kind),
                        Summary = classInfo.Summary,
                        Category = classInfo.MediaBlockCategory,
                        Score = score,
                    });
                }
            }
        }

        return results;
    }
}
