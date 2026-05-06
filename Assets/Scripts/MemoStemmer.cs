using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Lightweight morphological matcher used by the market screen to tint words
/// in material descriptions blue when they share a stem with the player's
/// memo. Pure-static, no dependencies, no allocations beyond the returned
/// string.
///
/// The stemmer is suffix-stripping (NOT full Porter): it catches the common
/// English variants — precision↔precise, calm↔calmly, flow↔flowing — without
/// the cost of an AI roundtrip. Synonyms (calm↔tranquil) are NOT caught;
/// that's a known trade-off documented in the plan.
/// </summary>
public static class MemoStemmer
{
    // Suffixes tried in priority order (longest first so "ization" wins over "ion").
    private static readonly string[] Suffixes =
    {
        "izations", "ization",
        "ations",   "ation",
        "tions",    "tion",
        "sions",    "sion",
        "ities",    "ity",
        "nesses",   "ness",
        "ments",    "ment",
        "ings",     "ing",
        "ous",
        "edly",     "ed",
        "ies",      "es",
        "ly",
        "est",      "er",
        "y",
        "s"
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","and","or","but","so","for","of","to","in","on","at","by","with",
        "from","into","onto","as","is","are","was","were","be","been","being","am",
        "do","does","did","have","has","had",
        "i","me","my","mine","you","your","yours","he","him","his","she","her","hers",
        "it","its","we","us","our","ours","they","them","their","theirs",
        "this","that","these","those","then","than","there","here",
        "what","which","who","whom","whose","when","where","why","how",
        "if","else","not","no","yes",
        "can","could","would","should","will","may","might","must","shall",
        "one","two","three","too","very","just","also","even","yet",
        "some","any","all","each","every","both","more","most","less","much","many","few"
    };

    private const int MinWordLen = 3;
    private const int MinStemLen = 3;

    /// <summary>
    /// Lowercase and strip a single longest matching suffix; if the result is
    /// shorter than MinStemLen, fall back to the lowercased original. Also
    /// drops a final "e" once if the post-strip stem still meets MinStemLen
    /// (so "precise" and "precision" both stem to "precis").
    /// </summary>
    public static string Stem(string word)
    {
        if (string.IsNullOrEmpty(word)) return "";
        string lower = word.ToLowerInvariant();
        if (lower.Length <= MinStemLen) return lower;

        foreach (var suffix in Suffixes)
        {
            if (lower.Length > suffix.Length + (MinStemLen - 1) && lower.EndsWith(suffix))
            {
                string stem = lower.Substring(0, lower.Length - suffix.Length);
                return TrimTerminalE(stem);
            }
        }
        return TrimTerminalE(lower);
    }

    private static string TrimTerminalE(string stem) =>
        stem.Length > MinStemLen && stem[stem.Length - 1] == 'e'
            ? stem.Substring(0, stem.Length - 1)
            : stem;

    /// <summary>
    /// Walk the four memo fields, split entries (the player's memo is comma-
    /// joined when multiple chips fill one slot), and return the unique stems
    /// of every content word.
    /// </summary>
    public static HashSet<string> BuildMemoStems(PlayerMemo memo)
    {
        var stems = new HashSet<string>(StringComparer.Ordinal);
        if (memo == null) return stems;

        AddFieldStems(stems, memo.element);
        AddFieldStems(stems, memo.personality);
        AddFieldStems(stems, memo.purpose);
        AddFieldStems(stems, memo.reinforcement);
        return stems;
    }

    private static void AddFieldStems(HashSet<string> stems, string field)
    {
        if (string.IsNullOrWhiteSpace(field)) return;
        // Memo entries are comma-joined when multiple chips sit in one slot
        // (see DossierPanelController.CheckMemoCompletion). The same separator
        // set IsHintMatch already handles is reused here for parity.
        var pieces = field.Split(new[] { ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var piece in pieces)
        {
            int i = 0;
            string p = piece;
            while (i < p.Length)
            {
                if (IsWordChar(p[i]))
                {
                    int s = i;
                    while (i < p.Length && IsWordChar(p[i])) i++;
                    string word = p.Substring(s, i - s);
                    if (word.Length >= MinWordLen && !StopWords.Contains(word))
                        stems.Add(Stem(word));
                }
                else i++;
            }
        }
    }

    /// <summary>
    /// Tokenize <paramref name="text"/>, stem each content word, and wrap
    /// matches in TMP <c>&lt;color=#hex&gt;...&lt;/color&gt;</c> tags. Non-word runs
    /// (whitespace, punctuation) and unmatched words pass through unchanged.
    /// </summary>
    public static string HighlightMatches(string text, HashSet<string> memoStems, string colorHex)
    {
        if (string.IsNullOrEmpty(text) || memoStems == null || memoStems.Count == 0)
            return text ?? "";

        var sb = new StringBuilder(text.Length + 32);
        int i = 0;
        while (i < text.Length)
        {
            if (IsWordChar(text[i]))
            {
                int s = i;
                while (i < text.Length && IsWordChar(text[i])) i++;
                string word = text.Substring(s, i - s);
                bool eligible = word.Length >= MinWordLen && !StopWords.Contains(word);
                if (eligible && memoStems.Contains(Stem(word)))
                {
                    sb.Append("<color=").Append(colorHex).Append('>');
                    sb.Append(word);
                    sb.Append("</color>");
                }
                else
                {
                    sb.Append(word);
                }
            }
            else
            {
                int s = i;
                while (i < text.Length && !IsWordChar(text[i])) i++;
                sb.Append(text, s, i - s);
            }
        }
        return sb.ToString();
    }

    private static bool IsWordChar(char c) => char.IsLetter(c) || c == '\'' || c == '-';
}
