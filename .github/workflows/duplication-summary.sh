#!/usr/bin/env bash
#
# Turns the JSON report that "jscpd --reporters json" writes into a human-readable job summary,
# and prints the same figures to the log. The sibling of coverage-summary.sh, deliberately built
# to the same shape: read the totals, render one table, never fail the build.
#
# WHY THIS EXISTS. Duplication was reported by nothing at all - not the workflows, not the csproj,
# not CodeQL (which runs security queries, not clone detection). The four duplicated helpers that
# .claude/CLAUDE.md warns about (ComputeWheelZoomFactor, GetImageContentRect, PixelToLocalRect and
# a second oscilloscope title-suffix stripper) were all found by hand, one at a time, after they
# had already shipped. A figure the pipeline recomputes on every push turns that into a trend
# somebody can watch instead of a discovery somebody has to make.
#
# It deliberately does NOT fail the build on a threshold, for the same reason the coverage script
# does not. This codebase carries a long tail of structurally similar UI code - four worklog
# dialogs sharing an Escape/Ctrl+Enter key handler, near-miss viewport helpers that differ by
# exactly the bounds check their names describe - which a clone detector reports and which nobody
# intends to change. A gate on that number would fire on things that are not defects, and a step
# that cries wolf is a step people stop reading. Making it a gate is a separate, deliberate
# decision.
#
# Usage: duplication-summary.sh <path-to-jscpd-report.json>

set -uo pipefail

REPORT="${1:-}"

if [ -z "$REPORT" ] || [ ! -f "$REPORT" ]; then
    echo "No duplication report found at '${REPORT}' - skipping the duplication summary."
    exit 0
fi

# The totals live under .statistics.total. Unlike the Cobertura reader beside this script, this
# one uses a real JSON parser rather than a grep: jscpd nests the same key names (clones,
# duplicatedLines, percentage) under .statistics.formats.<lang> as well as under .statistics.total,
# so a line-oriented match would happily report ONE LANGUAGE's numbers as the project total. That
# is the same silent-wrong-figure failure coverage-summary.sh guards against by isolating the root
# element first.
#
# The interpreter is resolved rather than hardcoded: it is "python3" on the Ubuntu runner, but on a
# Windows dev box "python3" is a Microsoft Store alias stub that prints an install message and
# exits non-zero, where the working interpreter is "python". Probing both keeps this script usable
# locally, which is the whole point of the GITHUB_STEP_SUMMARY guard further down too.
PYTHON=""
for candidate in python3 python; do
    if command -v "$candidate" >/dev/null 2>&1 && "$candidate" -c "" >/dev/null 2>&1; then
        PYTHON="$candidate"
        break
    fi
done

if [ -z "$PYTHON" ]; then
    echo "No working Python interpreter found - skipping the duplication summary."
    exit 0
fi

read_totals() {
    "$PYTHON" -c '
import json, sys

try:
    with open(sys.argv[1], encoding="utf-8") as handle:
        report = json.load(handle)
except (OSError, ValueError):
    sys.exit(1)

total = report.get("statistics", {}).get("total")
if not isinstance(total, dict):
    sys.exit(1)

# "clones" is a pair count, so a value of 0 is meaningful and must not be confused with a missing
# key - hence .get with an explicit default rather than a truthiness test.
print(total.get("clones", 0))
print(total.get("duplicatedLines", 0))
print(total.get("lines", 0))
print(total.get("duplicatedTokens", 0))
print(total.get("tokens", 0))
print(total.get("sources", 0))
' "$1"
}

TOTALS=$(read_totals "$REPORT") || {
    echo "Duplication report at '${REPORT}' carried no totals - skipping the summary."
    exit 0
}

CLONES=$(printf '%s' "$TOTALS" | sed -n '1p')
DUPLICATED_LINES=$(printf '%s' "$TOTALS" | sed -n '2p')
TOTAL_LINES=$(printf '%s' "$TOTALS" | sed -n '3p')
DUPLICATED_TOKENS=$(printf '%s' "$TOTALS" | sed -n '4p')
TOTAL_TOKENS=$(printf '%s' "$TOTALS" | sed -n '5p')
SOURCES=$(printf '%s' "$TOTALS" | sed -n '6p')

if [ -z "$TOTAL_LINES" ] || [ "$TOTAL_LINES" = "0" ]; then
    echo "Duplication report at '${REPORT}' analysed no lines - skipping the summary."
    exit 0
fi

percent() {
    awk -v part="$1" -v whole="$2" 'BEGIN { if (whole == 0) print "n/a"; else printf "%.2f", (part / whole) * 100 }'
}

LINE_PERCENT=$(percent "$DUPLICATED_LINES" "$TOTAL_LINES")
TOKEN_PERCENT=$(percent "$DUPLICATED_TOKENS" "$TOTAL_TOKENS")

echo "Files analysed:    ${SOURCES}"
echo "Clones found:      ${CLONES}"
echo "Duplicated lines:  ${LINE_PERCENT}% (${DUPLICATED_LINES}/${TOTAL_LINES})"
echo "Duplicated tokens: ${TOKEN_PERCENT}% (${DUPLICATED_TOKENS}/${TOTAL_TOKENS})"

# GITHUB_STEP_SUMMARY is absent when this is run by hand, which is why it is guarded rather than
# assumed - the script stays useful locally.
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    {
        echo "### Code duplication"
        echo
        echo "| Metric | Duplicated | Total | Share |"
        echo "| --- | ---: | ---: | ---: |"
        echo "| Lines | ${DUPLICATED_LINES} | ${TOTAL_LINES} | **${LINE_PERCENT}%** |"
        echo "| Tokens | ${DUPLICATED_TOKENS} | ${TOTAL_TOKENS} | ${TOKEN_PERCENT}% |"
        echo
        echo "${CLONES} clone(s) across ${SOURCES} analysed file(s) in \`Handlers/\`, \`Main/\` and \`Tabs/\`,"
        echo "at a 10-line minimum. Reported, never enforced - some of these are structural echo"
        echo "(dialogs sharing a key handler, helpers differing only by a bounds check) rather than"
        echo "defects. Download the \`duplication-report\` artifact for the per-clone file and line list."
    } >> "$GITHUB_STEP_SUMMARY"
fi
