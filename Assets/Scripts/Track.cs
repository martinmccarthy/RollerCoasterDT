using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class Track : MonoBehaviour
{
    Spline spline;

    public int samples = 6;
    public int railSides = 8;
    public float trackWidth = .6f;
    public float railRadius = 0.05f;
    public float tieSpace = 0.5f;
    public float tieHeight = 0.08f;
    public float tieWidth = 0.48f;
    public float tieDepth = 0.06f;

    private void Start()
    {
        spline = GetComponent<SplineContainer>().Spline;
        SampleSpline(out Vector3[] positions, out Vector3[] tangents, out Vector3[] ups, out Vector3[] rights);
        BuildMesh(positions, tangents, ups, rights);
    }

    private void SampleSpline(out Vector3[] positions, out Vector3[] tangents, out Vector3[] ups, out Vector3[] rights)
    {
        float length = spline.GetLength();
        int totalSamples = Mathf.Max(4, Mathf.RoundToInt(length * samples));

        positions = new Vector3[totalSamples];
        tangents = new Vector3[totalSamples];
        ups = new Vector3[totalSamples];
        rights = new Vector3[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (totalSamples - 1f);
            spline.Evaluate(t, out float3 position, out float3 tangent, out float3 up);

            positions[i] = (Vector3)position;
            tangents[i] = math.normalizesafe(tangent);
            ups[i] = math.normalizesafe(up);
            rights[i] = Vector3.Cross(tangents[i], ups[i]).normalized;
        }
    }

    private void BuildMesh(Vector3[] positions, Vector3[] tangents, Vector3[] ups, Vector3[] rights)
    {
        float halfWidth = trackWidth * 0.5f;

        (List<Vector3> rv1, List<int> rt1, List<Vector2> ru1) = BuildRail(positions, rights, ups, halfWidth);
        (List<Vector3> rv2, List<int> rt2, List<Vector2> ru2) = BuildRail(positions, rights, ups, -halfWidth);
        (List<Vector3> tv, List<int> tt, List<Vector2> tu) = BuildTies(positions, rights, ups);

        // combine into one mesh
        List<Vector3> verts = new();
        List<int> tris = new();
        List<Vector2> uvs = new();

        AppendMesh(verts, tris, uvs, rv1, rt1, ru1);
        AppendMesh(verts, tris, uvs, rv2, rt2, ru2);
        AppendMesh(verts, tris, uvs, tv, tt, tu);

        DrawMesh(verts, tris, uvs);
    }

    // offsets tri indices by current vert count before appending
    private void AppendMesh(
        List<Vector3> verts, List<int> tris, List<Vector2> uvs,
        List<Vector3> newVerts, List<int> newTris, List<Vector2> newUVs)
    {
        int offset = verts.Count;
        verts.AddRange(newVerts);
        uvs.AddRange(newUVs);
        foreach (int t in newTris)
            tris.Add(t + offset);
    }

    private (List<Vector3>, List<int>, List<Vector2>) BuildRail(
        Vector3[] positions, Vector3[] rights, Vector3[] ups, float halfWidth)
    {
        List<Vector3> verts = new();
        List<int> tris = new();
        List<Vector2> uvs = new();

        int sides = Mathf.Max(3, railSides);
        int count = positions.Length;
        float uStep = 1f / sides;

        // generate rings
        for (int i = 0; i < count; i++)
        {
            Vector3 centre = positions[i] + rights[i] * halfWidth;
            float v = i / (count - 1f);

            for (int s = 0; s < sides; s++)
            {
                float theta = s / (float)sides * Mathf.PI * 2f;
                Vector3 offset = (rights[i] * Mathf.Cos(theta) + ups[i] * Mathf.Sin(theta)) * railRadius;
                verts.Add(centre + offset);
                uvs.Add(new Vector2(s * uStep, v));
            }
        }

        // stitch rings into quads
        for (int i = 0; i < count - 1; i++)
        {
            for (int s = 0; s < sides; s++)
            {
                int sNext = (s + 1) % sides;

                int a = i * sides + s;
                int b = i * sides + sNext;
                int c = (i + 1) * sides + s;
                int d = (i + 1) * sides + sNext;

                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }

        return (verts, tris, uvs);
    }

    private (List<Vector3>, List<int>, List<Vector2>) BuildTies(Vector3[] positions, Vector3[] rights, Vector3[] ups)
    {
        List<Vector3> verts = new();
        List<int> tris = new();
        List<Vector2> uvs = new();

        float length = spline.GetLength();
        float inverse = 1f / length;
        float interval = tieSpace * 0.5f;

        while (interval < length)
        {
            float t = Mathf.Clamp01(interval * inverse);
            spline.Evaluate(t, out float3 position, out float3 tangent, out float3 up);

            Vector3 fwd = math.normalizesafe(tangent);
            Vector3 upN = math.normalizesafe(up);
            Vector3 right = Vector3.Cross(fwd, upN).normalized;
            Vector3 c = (Vector3)position - upN * (tieHeight * 0.5f);
            AddBox(verts, tris, uvs, c, right, upN, fwd, tieWidth, tieHeight, tieDepth);

            interval += tieSpace;
        }

        return (verts, tris, uvs);
    }

    private void AddBox(List<Vector3> verts, List<int> tris, List<Vector2> uvs, Vector3 center, Vector3 right, Vector3 up, Vector3 forward, float width, float height, float depth)
    {
        float hx = width * 0.5f;
        float hy = height * 0.5f;
        float hz = depth * 0.5f;

        Vector3[] corners = new Vector3[8]
        {
            center + (-right * hx) + (-up * hy) + (-forward * hz),
            center + ( right * hx) + (-up * hy) + (-forward * hz),
            center + ( right * hx) + ( up * hy) + (-forward * hz),
            center + (-right * hx) + ( up * hy) + (-forward * hz),
            center + (-right * hx) + (-up * hy) + ( forward * hz),
            center + ( right * hx) + (-up * hy) + ( forward * hz),
            center + ( right * hx) + ( up * hy) + ( forward * hz),
            center + (-right * hx) + ( up * hy) + ( forward * hz),
        };

        int[,] faces = new int[6, 4]
        {
            { 0, 1, 2, 3 },
            { 5, 4, 7, 6 },
            { 4, 0, 3, 7 },
            { 1, 5, 6, 2 },
            { 4, 5, 1, 0 },
            { 3, 2, 6, 7 },
        };

        Vector2[] faceUVs = { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };

        for (int f = 0; f < 6; f++)
        {
            int baseIdx = verts.Count;
            for (int v = 0; v < 4; v++)
            {
                verts.Add(corners[faces[f, v]]);
                uvs.Add(faceUVs[v]);
            }
            tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
            tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
        }
    }

    private void DrawMesh(List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        Mesh mesh = new();
        mesh.name = "TrackMesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var s = GetComponent<SplineContainer>().Spline;
        int steps = 64;
        Vector3 prev = transform.TransformPoint((Vector3)s.EvaluatePosition(0f));
        Gizmos.color = Color.yellow;
        for (int i = 1; i <= steps; i++)
        {
            Vector3 next = transform.TransformPoint((Vector3)s.EvaluatePosition((float)i / steps));
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}