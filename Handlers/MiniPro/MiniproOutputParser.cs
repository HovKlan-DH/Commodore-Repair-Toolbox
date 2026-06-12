// Parses minipro's logic-test output: the per-pin grid on stdout (ported from the
// PLA Doctor parser) and the "Logic test successful/failed" summary + connection
// errors on stderr. A clean pass on a large run prints NO per-vector lines (only
// failures are listed), so a missing grid + a "successful" summary IS a pass.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Handlers.IcTesting;

public static class MiniproOutputParser
{
    // Strip ANSI colour: ESC '[' ... 'm'. The ESC byte is required so we never
    // remove legitimate '[...]m' content or leave a stray ESC that shifts columns.
    private static readonly Regex Ansi = new(((char)0x1b) + "\\[[0-9;]*m", RegexOptions.Compiled);
    private static readonly Regex VectorLine = new(@"^\s*(\d+):\s?(.*)$", RegexOptions.Compiled);

    /// <summary>Strip ANSI colour codes so raw minipro output reads cleanly in a TextBox.</summary>
    public static string StripAnsi(string? s) => Ansi.Replace(s ?? "", "");

    public sealed record Parsed(
        bool? Passed,
        int VectorsSeen,
        IReadOnlyList<(int Vector, int Pin)> Failures,
        IReadOnlyList<int> FailingPins,
        MiniproConnectionState State,
        int? ErrorCount);

    public static Parsed Parse(string? stdout, string? stderr)
    {
        var failures = new List<(int, int)>();
        var failingPins = new SortedSet<int>();
        int vectorsSeen = 0;

        foreach (var raw in (stdout ?? "").Split('\n'))
        {
            var line = Ansi.Replace(raw, "").TrimEnd('\r');
            var m = VectorLine.Match(line);
            if (!m.Success) continue;                  // header / blank / non-vector line
            if (!int.TryParse(m.Groups[1].Value, out var vidx)) continue;
            vectorsSeen++;
            var body = m.Groups[2].Value;
            // Each pin field is 3 chars: symbol, marker, separator. marker '-' = error.
            for (int k = 0; k * 3 + 1 < body.Length; k++)
            {
                if (body[k * 3 + 1] == '-')
                {
                    int pin = k + 1;
                    failures.Add((vidx, pin));
                    failingPins.Add(pin);
                }
            }
        }

        var err = stderr ?? "";
        bool? passed = null;
        int? errCount = null;
        if (Regex.IsMatch(err, "logic test successful", RegexOptions.IgnoreCase))
            passed = true;
        var fm = Regex.Match(err, @"logic test failed:\s*(\d+)", RegexOptions.IgnoreCase);
        if (fm.Success) { passed = false; errCount = int.Parse(fm.Groups[1].Value); }
        else if (Regex.IsMatch(err, "logic test failed", RegexOptions.IgnoreCase)) { passed = false; }

        if (failures.Count > 0 && passed != false)
            passed = false;   // grid showed failures even if the summary was absent

        return new Parsed(passed, vectorsSeen, failures, new List<int>(failingPins),
            ClassifyState(err, passed), errCount);
    }

    public static MiniproConnectionState ClassifyState(string stderr, bool? passed)
    {
        var e = (stderr ?? "").ToLowerInvariant();
        if (e.Contains("no programmer") || e.Contains("programmer not found"))
            return MiniproConnectionState.NoProgrammer;
        if (e.Contains("overcurrent") || e.Contains("over-current") || e.Contains("over current"))
            return MiniproConnectionState.Overcurrent;
        if (e.Contains("device not found") || e.Contains("0x000000") || e.Contains("no device"))
            return MiniproConnectionState.NoChip;
        return passed.HasValue ? MiniproConnectionState.Ok : MiniproConnectionState.Unknown;
    }
}
