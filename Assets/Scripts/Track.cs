using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Track : MonoBehaviour
{
    #region Mesh Settings
    
    public float Resolution = 1.0f; // mesh fidelity, higher = smoother curves at cost of more polys
    

    #endregion
    
    MiniMesh railMesh;

    struct Vertex
    {
        public Vector3 pos, norm;
        public Vector2 uv;
    }

    // only need a few things from the mesh class (verts tris)
    class MiniMesh
    {
        public List<Vertex> verts = new();
        public List<int> tris = new();
    }

    Mesh generatedMesh;
    bool isDrawn = false;

    void Start()
    {
        if(!isDrawn)
        {
            DrawMesh();
        }
    }

    void DrawMesh()
    {
        generatedMesh = new Mesh();
        generatedMesh.name = "Rail";

    }

    // TODO: look into what "caps" are in the ZFTrack, not exactly sure what they do
    void SetMeshes(Mesh sourceRail, Mesh sourceTie)
    {
        railMesh = new();
        MiniMesh tieMesh = new();

        Vector3[] verts = sourceRail.vertices;
        Vector3[] norms = sourceRail.normals;
        Vector2[] uvs = sourceRail.uv;
        int[] tris = sourceRail.triangles;
        Dictionary<int, int> railTris = new();

        // maps an original mesh vertex index to the destination mesh, copying vertex on first use
        System.Func<int, Dictionary<int, int>, MiniMesh, int> mapVert = (v, mapping, dest) =>
        {
            int pos;
            if (mapping.TryGetValue(v, out pos)) return pos; // see if we've already copied this vert
            else // otherwise create a new vert
            {
                dest.verts.Add(new Vertex
                {
                    pos = verts[v],
                    norm = norms[v],
                    uv = uvs[v]
                });
                int newV = dest.verts.Count - 1;
                mapping[v] = newV;
                return newV;
            }
        };

        void AddFace(int v1, int v2, int v3, Dictionary<int, int> mapping, MiniMesh dest)
        {
            v1 = mapVert(v1, mapping, dest);
            v2 = mapVert(v2, mapping, dest);
            v3 = mapVert(v3, mapping, dest);

            dest.tris.Add(v1);
            dest.tris.Add(v2);
            dest.tris.Add(v3);
        }

        for(int i = 0; i < tris.Length; i += 3)
        {
            int v1 = tris[i], v2 = tris[i + 1], v3 = tris[i + 2];
            int sideCount = (verts[v1].z > 0 ? 0 : 1) + (verts[v2].z > 0 ? 0 : 1) + (verts[v3].z > 0 ? 0 : 1);
            if(sideCount == 0 || sideCount == 3)
            {
            }
            else
            {
                AddFace(v1, v2, v3, railTris, railMesh);
            }
        }
    }

    void GenerateMesh(Mesh mesh)
    {
        if(railMesh == null)
        {
            mesh.Clear();
            return;
        }

        List<Vector3> allVerts = new();
        List<Vector3> allNorms = new();
        List<Vector2> allUVs = new();
        List<int> allTris = new();

        int vertOffset = 0;
        // SimpleTransform
    }

    //float[] GetSteps()
    //{
    //    // int steps = (int)Mathf.Clamp(Mathf.Round())
    //}
}
