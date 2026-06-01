using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UIElements;

public class Track : MonoBehaviour
{
    Spline spline;

    // mesh generation
    public int samples = 6;
    public int railSides = 8;
    public float trackWidth = 1.435f;
    public float railRadius = 0.05f;
    public float tieSpace = 0.5f;
    public float tieHeight = 0.2f;
    public float tieWidth = 0.6f;
    public float tieDepth = 0.12f;

    private void Start()
    {
        spline = GetComponent<SplineContainer>().Splines[0];

        SampleSpline(out Vector3[] positions, out Vector3[] tangents, out Vector3[] ups, out Vector3[] rights);
        BuildMesh(positions, tangents, ups, rights);
    }

    private void SampleSpline(out Vector3[] positions, out Vector3[] tangents, out Vector3[] ups, out Vector3[] rights)
    {
        float length = spline.GetLength();

        // in regards to total samples here the reason for 4 is to make sure the mesh doesn't break, but it's arbitrary
        int totalSamples = Mathf.Max(4, Mathf.RoundToInt(length * samples));

        Debug.Log($"Length: {length}, Total Samples: {totalSamples}");

        positions = new Vector3[totalSamples];
        tangents = new Vector3[totalSamples];
        ups = new Vector3[totalSamples];
        rights = new Vector3[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (totalSamples - 1f);
            spline.Evaluate(t, out float3 position, out float3 tangent, out float3 up);
            positions[i] = (Vector3)position;
            tangents[i] = (Vector3)tangent;
            ups[i] = (Vector3)up;
            rights[i] = Vector3.Cross((Vector3)up, (Vector3)tangent).normalized;
        }

        Debug.Log($"{positions}, {tangents}, {ups}, {rights}");
    }

    private void BuildMesh(Vector3[] positions, Vector3[] tangents, Vector3[] ups, Vector3[] rights)
    {
        List<Vector3> verts = new();
        List<int> tris = new();
        List<Vector2> uvs = new();

        float halfWidth = trackWidth * 0.5f;
        BuildRail(verts, tris, uvs, positions, rights, ups, halfWidth);
        BuildRail(verts, tris, uvs, positions, rights, ups, -halfWidth);

        Debug.Log($"Verts: {verts.Count}, Tris: {tris.Count / 3}, UVs: {uvs.Count}");

        BuildTies(verts, tris, uvs, positions, rights, ups);
        DrawMesh(verts, tris.ToArray(), uvs);
    }

    private void BuildRail(List<Vector3> verts, List<int> tris, List<Vector2> uvs, Vector3[] positions, Vector3[] rights, Vector3[] ups, float halfWidth)
    {
        int sides = Mathf.Max(3, railSides);
        int samples = positions.Length;

        // When you call BuildRail() twice (once for each rail), the second call is appending vertices into the same verts list that already has the first rail's vertices in it.
        int baseIdx = verts.Count;

        float uStep = 1f / sides;

        for (int i = 0; i < samples; i++)
        {
            Vector3 c = positions[i] + rights[i] * halfWidth;

            float t = i / (samples - 1f);
            for (int side = 0; side < sides; side++)
            {
                float theta = side / sides * Mathf.PI * 2f;

                Vector3 offset = (rights[i] * Mathf.Cos(theta) + ups[i] * Mathf.Sin(theta)) * railRadius;

                verts.Add(c + offset);
                uvs.Add(new Vector2(side * uStep, t));
            }
        }

        for (int i = 0; i < samples - 1; i++)
        {
            for (int side = 0; side < sides; side++)
            {
                int nextSide = (side + 1) % sides;

                int a = baseIdx + i * sides + side;
                int b = baseIdx + i * sides + nextSide;
                int c = baseIdx + (i + 1) * sides + side;
                int d = baseIdx + (i + 1) * sides + nextSide;

                tris.Add(a);
                tris.Add(c);
                tris.Add(b);

                tris.Add(b);
                tris.Add(c);
                tris.Add(d);
            }
        }

        if (!spline.Closed)
            AddDiskCap();
    }

    // do this later im lazy af
    private void AddDiskCap()
    {

    }


    private void BuildTies(List<Vector3> verts, List<int> tris, List<Vector2> uvs, Vector3[] positions, Vector3[] rights, Vector3[] ups)
    {
        float length = spline.GetLength();
        int totalSamples = Mathf.Max(4, Mathf.RoundToInt(length * samples));

        float interval = tieSpace * 0.5f;
        float inverse = 1f / length;

        while (interval < length)
        {
            float t = Mathf.Clamp01(interval * inverse);

            spline.Evaluate(t, out float3 position, out float3 tangent, out float3 up);
            Vector3 fwd = math.normalizesafe(tangent);
            Vector3 upN = math.normalizesafe(up);
            Vector3 right = Vector3.Cross(upN, fwd).normalized;

            Vector3 c = (Vector3)position - upN * (railRadius + tieHeight * 0.5f);
            AddBox(verts, tris, uvs, c, right, upN, fwd, tieWidth, tieHeight, tieDepth);

            interval += tieSpace;
        }
    }

    private void AddBox(List<Vector3> verts, List<int> tris, List<Vector2> uvs, Vector3 center, Vector3 right, Vector3 up, Vector3 forward, float width, float height, float depth)
    {
        float hx = width * 0.5f;
        float hy = height * 0.5f;
        float hz = depth * 0.5f;

        Vector3[] corners = new Vector3[8]
        {
            center + (-right * hx) + (-up * hy) + (-forward * hz),
            center + (right * hx) + (-up * hy) + (-forward * hz),
            center + (right * hx) + (up * hy) + (-forward * hz),
            center + (-right * hx) + (up * hy) + (-forward * hz),
            center + (-right * hx) + (-up * hy) + (forward * hz),
            center + (right * hx) + (-up * hy) + (forward * hz),
            center + (right * hx) + (up * hy) + (forward * hz),
            center + (-right * hx) + (up * hy) + (forward * hz)
        };

        int[,] faces = new int[6, 4]
        {
            { 0, 1, 2, 3 }, // back   (-forward)
            { 5, 4, 7, 6 }, // front  (+forward)
            { 4, 0, 3, 7 }, // left   (-right)
            { 1, 5, 6, 2 }, // right  (+right)
            { 4, 5, 1, 0 }, // bottom (-up)
            { 3, 2, 6, 7 }, // top    (+up)
        };

        Vector2[] faceUVs = {
            new (0, 0),
            new (1, 0),
            new (1, 1),
            new (0, 1)
        };

        for (int i = 0; i < 6; i++)
        {
            int baseIdx = verts.Count;
            for (int j = 0; j < 4; j++)
            {
                verts.Add(corners[faces[i, j]]);
                uvs.Add(faceUVs[j]);
            }
            tris.Add(baseIdx);
            tris.Add(baseIdx + 2);
            tris.Add(baseIdx + 1);

            tris.Add(baseIdx);
            tris.Add(baseIdx + 3);
            tris.Add(baseIdx + 2);
        }
    }

    private void DrawMesh(List<Vector3> verts, int[] tris, List<Vector2> uvs)
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
        var spline = GetComponent<SplineContainer>().Spline;
        int steps = 64;
        Vector3 prev = transform.TransformPoint((Vector3)spline.EvaluatePosition(0f));
        Gizmos.color = Color.yellow;
        for (int i = 1; i <= steps; i++)
        {
            Vector3 next = transform.TransformPoint(
                (Vector3)spline.EvaluatePosition((float)i / steps));
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif

}
