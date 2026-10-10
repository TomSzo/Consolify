// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using System.Globalization;

namespace Consolify.Core.Measurement;

/// <summary>
/// Reads PresentMon CSV output. Columns are found by header name, so column order and extra columns
/// do not matter. Column names follow PresentMon 2.x (see README-ConsoleApplication.md in the
/// GameTechDev/PresentMon repository).
/// </summary>
public static class PresentMonCsv
{
    private const string ColApplication = "Application";
    private const string ColProcessId = "ProcessID";
    private const string ColBetweenPresents = "MsBetweenPresents";
    private const string ColBetweenDisplayChange = "MsBetweenDisplayChange";
    private const string ColDisplayedTime = "DisplayedTime";
    private const string ColFrameType = "FrameType";
    private const string ColPresentMode = "PresentMode";

    /// <summary>Parses a whole CSV.</summary>
    /// <exception cref="FormatException">A required column is missing.</exception>
    public static IReadOnlyList<FrameRecord> Parse(TextReader reader)
    {
        string? headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new FormatException("The PresentMon CSV is empty.");
        }

        var header = SplitLine(headerLine);
        var index = header
            .Select((name, i) => (name: name.Trim(), i))
            .GroupBy(x => x.name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);

        int app = Require(index, ColApplication);
        int pid = Require(index, ColProcessId);
        int betweenPresents = Require(index, ColBetweenPresents);
        int betweenDisplay = index.GetValueOrDefault(ColBetweenDisplayChange, -1);
        int displayedTime = index.GetValueOrDefault(ColDisplayedTime, -1);
        int frameType = index.GetValueOrDefault(ColFrameType, -1);
        int presentMode = index.GetValueOrDefault(ColPresentMode, -1);

        var records = new List<FrameRecord>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var cells = SplitLine(line);
            if (cells.Count <= Math.Max(app, Math.Max(pid, betweenPresents)))
            {
                continue;
            }

            double? present = ParseNumber(cells[betweenPresents]);
            if (present is null)
            {
                continue;
            }

            double? display = Cell(cells, betweenDisplay) is { } d ? ParseNumber(d) : null;
            string? shown = Cell(cells, displayedTime);
            bool displayed = shown is null
                ? display is > 0
                : !shown.Equals("NA", StringComparison.OrdinalIgnoreCase);

            records.Add(new FrameRecord(
                Application: cells[app],
                ProcessId: int.TryParse(cells[pid], NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) ? p : 0,
                MsBetweenPresents: present.Value,
                MsBetweenDisplayChange: display,
                Displayed: displayed,
                FrameType: Cell(cells, frameType),
                PresentMode: Cell(cells, presentMode)));
        }

        return records;
    }

    private static int Require(Dictionary<string, int> index, string column) =>
        index.TryGetValue(column, out int i)
            ? i
            : throw new FormatException($"The PresentMon CSV has no '{column}' column.");

    private static string? Cell(List<string> cells, int i) => i >= 0 && i < cells.Count ? cells[i] : null;

    private static double? ParseNumber(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;

    /// <summary>Splits one CSV line, honouring double-quoted fields.</summary>
    private static List<string> SplitLine(string line)
    {
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }
}
