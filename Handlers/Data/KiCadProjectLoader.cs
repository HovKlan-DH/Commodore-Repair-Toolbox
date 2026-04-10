using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Handlers.DataHandling
{
    // ###########################################################################################
    // Loads and caches one KiCad JSON export, then builds a schematic net-path index so the
    // Schematics tab can highlight both PCB copper and schematic wire geometry quickly.
    // ###########################################################################################
    internal static class KiCadProjectLoader
    {
        private static readonly Dictionary<string, KiCadProjectBundle?> thisCache =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly JsonSerializerOptions thisJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // ###########################################################################################
        // Streams and caches the KiCad JSON file from disk. Large files are read through FileStream
        // instead of File.ReadAllText so memory pressure stays reasonable.
        // ###########################################################################################
        public static async Task<KiCadProjectBundle?> LoadAsync(string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                return null;
            }

            if (KiCadProjectLoader.thisCache.TryGetValue(jsonPath, out var cached))
            {
                return cached;
            }

            if (!File.Exists(jsonPath))
            {
                KiCadProjectLoader.thisCache[jsonPath] = null;
                return null;
            }

            try
            {
                using var stream = new FileStream(
                    jsonPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    bufferSize: 131072,
                    useAsync: true);

                var root = await JsonSerializer.DeserializeAsync<KiCadProjectRoot>(
                    stream,
                    KiCadProjectLoader.thisJsonOptions).ConfigureAwait(false);

                if (root == null)
                {
                    Logger.Warning($"KiCad JSON could not be deserialized: [{jsonPath}]");
                    KiCadProjectLoader.thisCache[jsonPath] = null;
                    return null;
                }

                var bundle = new KiCadProjectBundle
                {
                    Root = root,
                    SchematicNetPathIndexBySchematicIndex =
                        KiCadProjectLoader.BuildSchematicNetPathIndex(root.Schematics)
                };

                KiCadProjectLoader.thisCache[jsonPath] = bundle;

                int pcbViewCount = root.Project.Views.Count(view =>
                    !string.IsNullOrWhiteSpace(view.Type) &&
                    view.Type.StartsWith("pcb", StringComparison.OrdinalIgnoreCase));

                int schematicViewCount = root.Project.Views.Count(view =>
                    string.Equals(view.Type, "schematic", StringComparison.OrdinalIgnoreCase));

                Logger.Info(
                    $"KiCad JSON loaded: [{jsonPath}] - PCB views [{pcbViewCount}], schematic views [{schematicViewCount}]");

                return bundle;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to load KiCad JSON [{jsonPath}] - [{ex.Message}]");
                KiCadProjectLoader.thisCache[jsonPath] = null;
                return null;
            }
        }

        // ###########################################################################################
        // Builds a per-schematic lookup of normalized net names to connected wire/polyline paths.
        // The MVP logic seeds paths from labels touching a path and then floods through endpoint-
        // connected paths. This is sufficient for most KiCad sheets that use standard labels.
        // ###########################################################################################
        private static Dictionary<int, Dictionary<string, List<KiCadResolvedPath>>> BuildSchematicNetPathIndex(
            IReadOnlyList<KiCadSchematic> schematics)
        {
            var result = new Dictionary<int, Dictionary<string, List<KiCadResolvedPath>>>();

            for (int schematicIndex = 0; schematicIndex < schematics.Count; schematicIndex++)
            {
                var schematic = schematics[schematicIndex];

                var sourcePaths = schematic.Wires
                    .Concat(schematic.Polylines)
                    .Where(path => path.Points.Count >= 2)
                    .Select(path => new IndexedPath
                    {
                        Points = path.Points
                            .Select(point => new KiCadPoint2D
                            {
                                X = point.X,
                                Y = point.Y
                            })
                            .ToList()
                    })
                    .ToList();

                if (sourcePaths.Count == 0)
                {
                    result[schematicIndex] =
                        new Dictionary<string, List<KiCadResolvedPath>>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                var adjacency = KiCadProjectLoader.BuildEndpointAdjacency(sourcePaths);
                var indexByNet = new Dictionary<string, List<KiCadResolvedPath>>(StringComparer.OrdinalIgnoreCase);

                foreach (var label in KiCadProjectLoader.EnumerateNetLabels(schematic))
                {
                    string normalizedText = label.NormalizedText?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(normalizedText) || label.At == null)
                    {
                        continue;
                    }

                    var seedPathIndexes = new List<int>();

                    for (int i = 0; i < sourcePaths.Count; i++)
                    {
                        if (KiCadProjectLoader.PathContainsPoint(
                            sourcePaths[i].Points,
                            label.At.X,
                            label.At.Y,
                            0.35))
                        {
                            seedPathIndexes.Add(i);
                        }
                    }

                    if (seedPathIndexes.Count == 0)
                    {
                        continue;
                    }

                    if (!indexByNet.TryGetValue(normalizedText, out var targetList))
                    {
                        targetList = new List<KiCadResolvedPath>();
                        indexByNet[normalizedText] = targetList;
                    }

                    var visited = new HashSet<int>();
                    var queue = new Queue<int>(seedPathIndexes);

                    while (queue.Count > 0)
                    {
                        int current = queue.Dequeue();
                        if (!visited.Add(current))
                        {
                            continue;
                        }

                        targetList.Add(new KiCadResolvedPath
                        {
                            Points = sourcePaths[current].Points
                                .Select(point => new KiCadPoint2D
                                {
                                    X = point.X,
                                    Y = point.Y
                                })
                                .ToList()
                        });

                        foreach (int adjacent in adjacency[current])
                        {
                            if (!visited.Contains(adjacent))
                            {
                                queue.Enqueue(adjacent);
                            }
                        }
                    }
                }

                result[schematicIndex] = indexByNet;
            }

            return result;
        }

        // ###########################################################################################
        // Connects path indexes that share exact or near-exact endpoints. This is the fast backbone
        // used by the schematic net flood-fill.
        // ###########################################################################################
        private static List<HashSet<int>> BuildEndpointAdjacency(IReadOnlyList<IndexedPath> paths)
        {
            const double endpointTolerance = 0.02;

            var adjacency = new List<HashSet<int>>(paths.Count);
            for (int i = 0; i < paths.Count; i++)
            {
                adjacency.Add(new HashSet<int>());
            }

            var endpointBuckets = new Dictionary<string, List<int>>(StringComparer.Ordinal);

            for (int i = 0; i < paths.Count; i++)
            {
                var first = paths[i].Points.First();
                var last = paths[i].Points.Last();

                KiCadProjectLoader.AddEndpointBucket(
                    endpointBuckets,
                    KiCadProjectLoader.MakePointKey(first.X, first.Y, endpointTolerance),
                    i);

                KiCadProjectLoader.AddEndpointBucket(
                    endpointBuckets,
                    KiCadProjectLoader.MakePointKey(last.X, last.Y, endpointTolerance),
                    i);
            }

            foreach (var bucket in endpointBuckets.Values)
            {
                if (bucket.Count < 2)
                {
                    continue;
                }

                for (int i = 0; i < bucket.Count; i++)
                {
                    for (int j = i + 1; j < bucket.Count; j++)
                    {
                        adjacency[bucket[i]].Add(bucket[j]);
                        adjacency[bucket[j]].Add(bucket[i]);
                    }
                }
            }

            return adjacency;
        }

        // ###########################################################################################
        // Returns all electrical label types that can identify a net on a schematic page.
        // ###########################################################################################
        private static IEnumerable<KiCadSchematicLabel> EnumerateNetLabels(KiCadSchematic schematic)
        {
            foreach (var item in schematic.Labels.Local)
            {
                yield return item;
            }

            foreach (var item in schematic.Labels.Global)
            {
                yield return item;
            }

            foreach (var item in schematic.Labels.Hierarchical)
            {
                yield return item;
            }
        }

        // ###########################################################################################
        // Returns true when the supplied point lies on any segment inside the given path.
        // ###########################################################################################
        private static bool PathContainsPoint(
            IReadOnlyList<KiCadPoint2D> points,
            double x,
            double y,
            double tolerance)
        {
            if (points.Count < 2)
            {
                return false;
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                double distance = KiCadProjectLoader.DistancePointToSegment(
                    x,
                    y,
                    points[i].X,
                    points[i].Y,
                    points[i + 1].X,
                    points[i + 1].Y);

                if (distance <= tolerance)
                {
                    return true;
                }
            }

            return false;
        }

        // ###########################################################################################
        // Returns the shortest distance between one point and one line segment.
        // ###########################################################################################
        private static double DistancePointToSegment(
            double px,
            double py,
            double x1,
            double y1,
            double x2,
            double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;

            if (Math.Abs(dx) < 1e-12 && Math.Abs(dy) < 1e-12)
            {
                double onlyDx = px - x1;
                double onlyDy = py - y1;
                return Math.Sqrt((onlyDx * onlyDx) + (onlyDy * onlyDy));
            }

            double t = (((px - x1) * dx) + ((py - y1) * dy)) / ((dx * dx) + (dy * dy));
            t = Math.Clamp(t, 0.0, 1.0);

            double cx = x1 + (t * dx);
            double cy = y1 + (t * dy);

            double ddx = px - cx;
            double ddy = py - cy;

            return Math.Sqrt((ddx * ddx) + (ddy * ddy));
        }

        // ###########################################################################################
        // Quantizes one point into a stable string key so near-identical coordinates collapse into
        // the same endpoint bucket.
        // ###########################################################################################
        private static string MakePointKey(double x, double y, double tolerance)
        {
            long qx = (long)Math.Round(x / tolerance, MidpointRounding.AwayFromZero);
            long qy = (long)Math.Round(y / tolerance, MidpointRounding.AwayFromZero);
            return $"{qx}:{qy}";
        }

        // ###########################################################################################
        // Adds one path index into the specified endpoint bucket.
        // ###########################################################################################
        private static void AddEndpointBucket(
            Dictionary<string, List<int>> buckets,
            string key,
            int pathIndex)
        {
            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = new List<int>();
                buckets[key] = bucket;
            }

            bucket.Add(pathIndex);
        }

        private sealed class IndexedPath
        {
            public List<KiCadPoint2D> Points { get; init; } = new();
        }
    }
}