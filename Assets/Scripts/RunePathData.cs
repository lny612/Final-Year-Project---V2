using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static rune path definitions and math utilities for the tracing minigame.
/// Each rune is a set of control points in normalized (0–1) space, interpolated
/// at runtime via Catmull-Rom into a smooth dense path.
/// </summary>
public static class RunePathData
{
    // ── Rune shape definitions (normalized 0–1 space) ─────────────

    public struct RuneShape
    {
        public Vector2[] ControlPoints;
        public float[]   WaypointTs;   // normalized T positions for hold-waypoints
    }

    // Rune shapes derived from orchestral conducting beat patterns.
    // Each shape is a SINGLE-measure baton gesture — redesigned so the trail
    // never doubles back on itself (no "double-circle" overlap). Gate waypoints
    // land on the ictus points (the strong articulation instants where the
    // baton reverses direction). 5 gates per round: combinations of the main
    // ictuses plus prep or subdivision cues where fewer than 5 ictuses exist.
    public static readonly RuneShape[] ALL_SHAPES = new RuneShape[]
    {
        // 0 — "Maestoso 4/4" (common time, one measure)
        // Classic down–left–right–up conducting cross. Diagonal prep from the
        // upper-left so the descent isn't a vertical line that would be
        // re-crossed later by the ascent.
        new RuneShape
        {
            ControlPoints = new Vector2[]
            {
                new(0.30f, 0.85f),  // prep (upper-left)
                new(0.50f, 0.15f),  // b1 ictus — bottom center
                new(0.32f, 0.30f),
                new(0.12f, 0.40f),  // b2 ictus — low-left
                new(0.28f, 0.55f),
                new(0.50f, 0.58f),  // apex (above body center)
                new(0.72f, 0.55f),
                new(0.88f, 0.40f),  // b3 ictus — low-right
                new(0.80f, 0.62f),
                new(0.70f, 0.82f),
                new(0.55f, 0.92f),  // b4 ictus — top
            },
            WaypointTs = new float[] { 0.06f, 0.22f, 0.44f, 0.72f, 0.96f }
        },
        // 1 — "Valse 3/4" (waltz triple meter, one measure)
        // Triangular conducting pattern: down → low-right → top. Five gates
        // from the three ictuses plus two "&" subdivision cues on the rebounds.
        new RuneShape
        {
            ControlPoints = new Vector2[]
            {
                new(0.35f, 0.85f),  // prep (upper-left)
                new(0.50f, 0.12f),  // b1 ictus — down
                new(0.62f, 0.22f),  // & of 1 — rebound
                new(0.78f, 0.38f),
                new(0.88f, 0.55f),  // b2 ictus — low-right
                new(0.78f, 0.72f),
                new(0.62f, 0.85f),  // & of 2 — rebound
                new(0.45f, 0.92f),  // b3 ictus — top
            },
            WaypointTs = new float[] { 0.18f, 0.32f, 0.58f, 0.80f, 0.96f }
        },
        // 2 — "Compound 6/8" (one measure, two-lobe "in 2" pattern)
        // Compound duple conducted in 2 with subdivided lobes: the baton
        // traces a bottom-left lobe, crosses center, then a bottom-right lobe,
        // rising to the top. Never returns to the start — no overlap.
        new RuneShape
        {
            ControlPoints = new Vector2[]
            {
                new(0.50f, 0.90f),  // prep (top center)
                new(0.25f, 0.65f),
                new(0.15f, 0.45f),  // b1 main ictus — left lobe low
                new(0.25f, 0.28f),
                new(0.35f, 0.18f),  // & of 1 — inner rebound
                new(0.42f, 0.30f),
                new(0.50f, 0.45f),  // lobe transition (center)
                new(0.58f, 0.30f),
                new(0.65f, 0.18f),  // & of 2 — mirror inner rebound
                new(0.75f, 0.28f),
                new(0.85f, 0.45f),  // b2 main ictus — right lobe low
                new(0.75f, 0.65f),
                new(0.60f, 0.85f),  // finish (offset from prep)
            },
            WaypointTs = new float[] { 0.18f, 0.32f, 0.58f, 0.72f, 0.96f }
        },
        // 3 — "Take Five 5/4" (asymmetric quintuple, 3+2 grouping)
        // Five uneven beats in one sweep — natural 5-ictus → 5-gate mapping.
        // Starts upper-left, sweeps through the five ictuses, exits at top.
        new RuneShape
        {
            ControlPoints = new Vector2[]
            {
                new(0.40f, 0.88f),  // prep (upper-left)
                new(0.50f, 0.12f),  // b1 ictus — down
                new(0.32f, 0.22f),
                new(0.15f, 0.35f),  // b2 ictus — low-left
                new(0.32f, 0.48f),
                new(0.55f, 0.50f),  // b3 ictus — center-across
                new(0.72f, 0.48f),
                new(0.88f, 0.32f),  // b4 ictus — low-right (start of 2-group)
                new(0.80f, 0.55f),
                new(0.65f, 0.78f),
                new(0.50f, 0.92f),  // b5 ictus — top
            },
            WaypointTs = new float[] { 0.14f, 0.32f, 0.52f, 0.70f, 0.96f }
        },
        // 4 — "Quick March 2/4" (one measure, tall down-up oval)
        // Brisk duple meter. Only 2 ictuses per measure, padded with two "&"
        // subdivisions plus a release cue for five gates. The figure is a
        // tall, narrow downstroke-and-upstroke oval that ends low-right.
        new RuneShape
        {
            ControlPoints = new Vector2[]
            {
                new(0.28f, 0.85f),  // prep (upper-left)
                new(0.50f, 0.12f),  // b1 ictus — down
                new(0.38f, 0.32f),
                new(0.22f, 0.48f),  // & of 1
                new(0.38f, 0.65f),
                new(0.50f, 0.85f),  // b2 ictus — up
                new(0.62f, 0.65f),
                new(0.78f, 0.48f),  // & of 2
                new(0.85f, 0.28f),
                new(0.82f, 0.12f),  // release (final)
            },
            WaypointTs = new float[] { 0.12f, 0.30f, 0.52f, 0.72f, 0.96f }
        },
        // 5 — "Fermata Crescendo" (expressive freeform cue)
        // A monotonic rising gesture: soft hold at the opening, then a steady
        // crescendo ascent across the panel, ending in a high release flourish.
        // No back-tracking — the strictest single-sweep in the set.
        new RuneShape
        {
            ControlPoints = new Vector2[]
            {
                new(0.15f, 0.35f),  // start — pianissimo opening
                new(0.22f, 0.30f),  // cue 1 — fermata hold (dip)
                new(0.30f, 0.32f),
                new(0.38f, 0.40f),  // cue 2 — begin crescendo lift
                new(0.45f, 0.50f),
                new(0.55f, 0.60f),  // cue 3 — sustain
                new(0.65f, 0.68f),
                new(0.75f, 0.78f),  // cue 4 — fortissimo peak
                new(0.82f, 0.85f),
                new(0.78f, 0.92f),
                new(0.65f, 0.90f),  // cue 5 — release flourish
            },
            WaypointTs = new float[] { 0.08f, 0.28f, 0.50f, 0.72f, 0.96f }
        },
    };

    // ── Path interpolation (Catmull-Rom) ──────────────────────────

    /// <summary>
    /// Subdivide control points into a smooth path using Catmull-Rom splines.
    /// Returns a dense array of points (~subdivisions points per segment).
    /// </summary>
    public static Vector2[] Interpolate(Vector2[] controlPoints, int subdivisions = 6)
    {
        if (controlPoints.Length < 2)
            return (Vector2[])controlPoints.Clone();

        var result = new List<Vector2>();

        for (int i = 0; i < controlPoints.Length - 1; i++)
        {
            Vector2 p0 = controlPoints[Mathf.Max(i - 1, 0)];
            Vector2 p1 = controlPoints[i];
            Vector2 p2 = controlPoints[Mathf.Min(i + 1, controlPoints.Length - 1)];
            Vector2 p3 = controlPoints[Mathf.Min(i + 2, controlPoints.Length - 1)];

            for (int j = 0; j < subdivisions; j++)
            {
                float t = j / (float)subdivisions;
                result.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }
        result.Add(controlPoints[controlPoints.Length - 1]);
        return result.ToArray();
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    // ── Cumulative distance table ─────────────────────────────────

    /// <summary>
    /// Build a cumulative distance array. cumulDist[i] = total arc length from
    /// point 0 to point i. Used to convert between index and normalized T.
    /// </summary>
    public static float[] BuildCumulativeDistances(Vector2[] path)
    {
        float[] d = new float[path.Length];
        d[0] = 0f;
        for (int i = 1; i < path.Length; i++)
            d[i] = d[i - 1] + Vector2.Distance(path[i - 1], path[i]);
        return d;
    }

    /// <summary>Total arc length of the path in whatever units the points use.</summary>
    public static float PathLength(float[] cumulDist)
    {
        return cumulDist[cumulDist.Length - 1];
    }

    // ── Sampling ──────────────────────────────────────────────────

    /// <summary>
    /// Return the position at normalized t (0–1) along the path,
    /// measured by arc length.
    /// </summary>
    public static Vector2 SampleAt(Vector2[] path, float[] cumulDist, float t)
    {
        float totalLen = cumulDist[cumulDist.Length - 1];
        float targetDist = Mathf.Clamp01(t) * totalLen;

        for (int i = 1; i < cumulDist.Length; i++)
        {
            if (cumulDist[i] >= targetDist)
            {
                float segLen = cumulDist[i] - cumulDist[i - 1];
                if (segLen < 0.0001f) return path[i];
                float localT = (targetDist - cumulDist[i - 1]) / segLen;
                return Vector2.Lerp(path[i - 1], path[i], localT);
            }
        }
        return path[path.Length - 1];
    }

    // ── Closest point (forward-only) ──────────────────────────────

    /// <summary>
    /// Find the normalized T of the closest point on the path that is at or
    /// ahead of <paramref name="minT"/>. Returns -1 if the point is beyond
    /// the tolerance distance from the path.
    /// </summary>
    public static float ClosestTAhead(Vector2[] path, float[] cumulDist,
        Vector2 localPoint, float minT, float toleranceSq)
    {
        float totalLen = cumulDist[cumulDist.Length - 1];
        if (totalLen < 0.0001f) return minT;

        float bestDistSq = float.MaxValue;
        int   bestIdx    = -1;

        // Start searching from the segment that corresponds to minT
        int startIdx = TToIndex(cumulDist, minT);

        for (int i = startIdx; i < path.Length; i++)
        {
            float dSq = (path[i] - localPoint).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                bestIdx = i;
            }
        }

        if (bestIdx < 0 || bestDistSq > toleranceSq) return -1f;

        return cumulDist[bestIdx] / totalLen;
    }

    /// <summary>Convert normalized T to the nearest path index.</summary>
    public static int TToIndex(float[] cumulDist, float t)
    {
        float totalLen = cumulDist[cumulDist.Length - 1];
        float targetDist = Mathf.Clamp01(t) * totalLen;
        for (int i = 1; i < cumulDist.Length; i++)
        {
            if (cumulDist[i] >= targetDist) return Mathf.Max(0, i - 1);
        }
        return cumulDist.Length - 1;
    }

    // ── Self-overlap validation ───────────────────────────────────

    /// <summary>
    /// Count sample-point pairs separated by at least arcLengthGap of arc
    /// length that fall within minSelfDistance of each other. A single
    /// perpendicular crossing typically produces 1–3 pairs; a long parallel
    /// run (the "double-circle" failure mode) produces many more. Thresholds
    /// are expressed in normalized 0–1 path-space so they're resolution-free.
    /// </summary>
    public static int CountOverlapPairs(
        Vector2[] path, float[] cumulDist,
        float minSelfDistance, float arcLengthGap)
    {
        float total = PathLength(cumulDist);
        if (total < 0.0001f) return 0;
        float minSq   = minSelfDistance * minSelfDistance;
        float gapDist = arcLengthGap * total;

        int count = 0;
        for (int i = 0; i < path.Length; i++)
        {
            for (int j = i + 1; j < path.Length; j++)
            {
                if (cumulDist[j] - cumulDist[i] < gapDist) continue;
                if ((path[i] - path[j]).sqrMagnitude < minSq) count++;
            }
        }
        return count;
    }

    /// <summary>
    /// True if the path has more overlapping sample pairs than maxPairs.
    /// Default thresholds tolerate a single perpendicular self-crossing but
    /// reject any path that runs parallel to itself for a noticeable stretch.
    /// </summary>
    public static bool HasSelfOverlap(
        Vector2[] path, float[] cumulDist,
        float minSelfDistance = 0.04f,
        float arcLengthGap    = 0.15f,
        int   maxPairs        = 6)
    {
        return CountOverlapPairs(path, cumulDist, minSelfDistance, arcLengthGap) > maxPairs;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Editor / dev-build only: check every rune shape for self-overlap and
    /// log a warning when a pattern trips the validator. Runs once on class
    /// initialization — zero cost in shipped builds.
    /// </summary>
    public static void ValidateAll()
    {
        for (int i = 0; i < ALL_SHAPES.Length; i++)
        {
            Vector2[] path = Interpolate(ALL_SHAPES[i].ControlPoints);
            float[]   cum  = BuildCumulativeDistances(path);
            int pairs = CountOverlapPairs(path, cum, 0.04f, 0.15f);
            if (pairs > 6)
            {
                Debug.LogWarning(
                    $"[RunePathData] Shape {i} has {pairs} overlapping sample pairs — path appears to double back on itself.");
            }
        }
    }

    static RunePathData() { ValidateAll(); }
#endif

    // ── Selection ─────────────────────────────────────────────────

    /// <summary>
    /// Pick 3 non-repeating rune shapes, interpolate them, and return
    /// both the dense path and the waypoint T-values for each.
    /// </summary>
    public static void PickThreeUnique(
        out Vector2[][] paths,
        out float[][]   waypointTs)
    {
        var indices = new List<int>();
        for (int i = 0; i < ALL_SHAPES.Length; i++) indices.Add(i);

        // Fisher-Yates shuffle
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        paths      = new Vector2[3][];
        waypointTs = new float[3][];

        for (int r = 0; r < 3; r++)
        {
            var shape = ALL_SHAPES[indices[r]];
            paths[r]      = Interpolate(shape.ControlPoints);
            waypointTs[r]  = (float[])shape.WaypointTs.Clone();
        }
    }
}
