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
    // Each shape is the baton's path for a meter; waypoint T-values land on
    // the ictus points (the strong articulation instants where the baton
    // reverses direction). 5 gates per round keeps the conducting feel
    // demanding — every measure you must "strike the downbeat" with a key.
    public static readonly RuneShape[] ALL_SHAPES = new RuneShape[]
    {
        // 0 — "Maestoso 4/4" (common time, 2 measures)
        // Classic 4/4 conducting pattern: down → inward → outward → up, twice.
        new RuneShape
        {
            ControlPoints = new Vector2[]
            {
                new(0.50f, 0.88f), new(0.50f, 0.60f),
                // Measure 1
                new(0.50f, 0.15f),  // beat 1 — ICTUS (down)
                new(0.40f, 0.32f),
                new(0.22f, 0.48f),  // beat 2 — ICTUS (inward)
                new(0.40f, 0.50f), new(0.58f, 0.50f),
                new(0.78f, 0.48f),  // beat 3 — ICTUS (outward)
                new(0.62f, 0.68f),
                new(0.50f, 0.82f),  // beat 4 — top rebound
                // Measure 2
                new(0.50f, 0.55f), new(0.50f, 0.18f),  // m2 beat 1 — ICTUS
                new(0.38f, 0.35f),
                new(0.25f, 0.52f),  // m2 beat 2 — ICTUS
                new(0.45f, 0.52f), new(0.60f, 0.52f),
                new(0.75f, 0.50f),  // m2 beat 3 — ICTUS
                new(0.62f, 0.72f),
                new(0.50f, 0.92f),  // m2 beat 4 — final ictus
            },
            WaypointTs = new float[] { 0.12f, 0.24f, 0.40f, 0.66f, 0.88f }
        },
        // 1 — "Valse 3/4" (waltz triple meter, 2 measures)
        // Triangular conducting pattern: down → out-right → up, twice.
        new RuneShape
        {
            ControlPoints = new Vector2[]
            {
                new(0.50f, 0.88f), new(0.50f, 0.55f),
                // Measure 1
                new(0.50f, 0.15f),  // beat 1 — ICTUS (down)
                new(0.65f, 0.30f),
                new(0.80f, 0.50f),  // beat 2 — ICTUS (right)
                new(0.68f, 0.68f),
                new(0.52f, 0.85f),  // beat 3 — top
                // Measure 2
                new(0.50f, 0.55f),
                new(0.50f, 0.18f),  // m2 beat 1 — ICTUS
                new(0.66f, 0.30f),
                new(0.78f, 0.52f),  // m2 beat 2 — ICTUS
                new(0.66f, 0.72f),
                new(0.50f, 0.92f),  // m2 beat 3 — ICTUS (final)
            },
            WaypointTs = new float[] { 0.14f, 0.28f, 0.58f, 0.74f, 0.94f }
        },
        // 2 — "Compound 6/8" (one measure, 6 subdivided beats)
        // Compound duple: two main ictus groups (L and R), each holding 3
        // sub-beats. Baton traces a butterfly / two-leaf shape.
        new RuneShape
        {
            ControlPoints = new Vector2[]
            {
                new(0.50f, 0.88f),  // prep
                new(0.35f, 0.60f),
                new(0.22f, 0.30f),  // beat 1 — main L down ICTUS
                new(0.28f, 0.15f),
                new(0.38f, 0.20f),  // beat 2 — sub ICTUS
                new(0.44f, 0.30f),
                new(0.50f, 0.42f),  // beat 3 — center ICTUS
                new(0.56f, 0.30f),
                new(0.62f, 0.20f),  // beat 4 — mirror sub
                new(0.72f, 0.15f),
                new(0.78f, 0.30f),  // beat 5 — main R down ICTUS
                new(0.65f, 0.55f),
                new(0.55f, 0.75f),
                new(0.50f, 0.92f),  // beat 6 — top ICTUS
            },
            WaypointTs = new float[] { 0.15f, 0.30f, 0.46f, 0.70f, 0.93f }
        },
        // 3 — "Take Five 5/4" (asymmetric quintuple, 3+2 grouping)
        // Five uneven beats in one sweep — like Brubeck's iconic 5/4.
        new RuneShape
        {
            ControlPoints = new Vector2[]
            {
                new(0.50f, 0.88f), new(0.50f, 0.55f),
                new(0.48f, 0.15f),  // beat 1 — down ICTUS
                new(0.35f, 0.28f),
                new(0.20f, 0.45f),  // beat 2 — inward ICTUS
                new(0.35f, 0.52f),
                new(0.52f, 0.50f),  // beat 3 — center pivot ICTUS
                new(0.68f, 0.52f),
                new(0.82f, 0.45f),  // beat 4 — outward ICTUS
                new(0.70f, 0.70f),
                new(0.52f, 0.85f),
                new(0.48f, 0.92f),  // beat 5 — top ICTUS
            },
            WaypointTs = new float[] { 0.15f, 0.32f, 0.50f, 0.68f, 0.92f }
        },
        // 4 — "Quick March 2/4" (3 measures, striding across the stage)
        // Simple duple march, shifted horizontally each measure like a
        // procession moving across the screen — brisk, steady downbeats.
        new RuneShape
        {
            ControlPoints = new Vector2[]
            {
                new(0.18f, 0.85f), new(0.18f, 0.45f),
                new(0.20f, 0.18f),  // m1 beat 1 — down ICTUS
                new(0.22f, 0.45f),
                new(0.32f, 0.82f),  // m1 beat 2 — up
                new(0.38f, 0.45f),
                new(0.40f, 0.15f),  // m2 beat 1 — down ICTUS
                new(0.44f, 0.45f),
                new(0.54f, 0.85f),  // m2 beat 2 — up ICTUS
                new(0.60f, 0.45f),
                new(0.62f, 0.15f),  // m3 beat 1 — down ICTUS
                new(0.66f, 0.45f),
                new(0.76f, 0.82f),  // m3 beat 2 — up
                new(0.82f, 0.45f),
                new(0.82f, 0.18f),  // coda — final ICTUS
            },
            WaypointTs = new float[] { 0.14f, 0.30f, 0.48f, 0.68f, 0.93f }
        },
        // 5 — "Fermata Crescendo" (expressive freeform cue)
        // A single sweeping gesture: pianissimo lift → subito → drop into
        // piano → rebuild → fortissimo climax. Five expressive cue points.
        new RuneShape
        {
            ControlPoints = new Vector2[]
            {
                new(0.15f, 0.25f),  // start (piano)
                new(0.20f, 0.45f),
                new(0.22f, 0.70f),  // cue 1 — pianissimo lift
                new(0.32f, 0.82f),
                new(0.45f, 0.78f),  // cue 2 — subito
                new(0.48f, 0.60f),
                new(0.48f, 0.38f),  // cue 3 — drop
                new(0.55f, 0.22f),
                new(0.68f, 0.30f),  // cue 4 — rebuild
                new(0.75f, 0.48f),
                new(0.72f, 0.68f),
                new(0.65f, 0.82f),
                new(0.80f, 0.88f),  // cue 5 — fortissimo
            },
            WaypointTs = new float[] { 0.16f, 0.32f, 0.48f, 0.66f, 0.92f }
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
