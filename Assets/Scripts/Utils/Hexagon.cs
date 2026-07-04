using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Hexagon : MonoBehaviour
{
    public float radius = 1f;
    public float thickness = 0.5f;

    private void Awake()
    {
        Build();
    }

    [ContextMenu("Rebuild")]
    public void Build()
    {
        float t = thickness;

        Vector3[] ring = new Vector3[6];
        for (int k = 0; k < 6; k++)
        {
            float a = (60f * k + 30f) * Mathf.Deg2Rad;
            ring[k] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }

        var verts = new System.Collections.Generic.List<Vector3>();
        var tris  = new System.Collections.Generic.List<int>();

        int topCenter = verts.Count; verts.Add(new Vector3(0f, t, 0f));
        int topStart = verts.Count;
        for (int k = 0; k < 6; k++) verts.Add(ring[k] + Vector3.up * t);
        for (int k = 0; k < 6; k++)
        {
            tris.Add(topCenter);
            tris.Add(topStart + (k + 1) % 6);
            tris.Add(topStart + k);
        }

        int botCenter = verts.Count; verts.Add(Vector3.zero);
        int botStart = verts.Count;
        for (int k = 0; k < 6; k++) verts.Add(ring[k]);
        for (int k = 0; k < 6; k++)
        {
            tris.Add(botCenter);
            tris.Add(botStart + k);
            tris.Add(botStart + (k + 1) % 6);
        }

        for (int k = 0; k < 6; k++)
        {
            int k1 = (k + 1) % 6;
            int i = verts.Count;
            verts.Add(ring[k]);                    // b_k
            verts.Add(ring[k] + Vector3.up * t);   // t_k
            verts.Add(ring[k1] + Vector3.up * t);  // t_k+1
            verts.Add(ring[k1]);                   // b_k+1

            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        var mesh = new Mesh { name = "ProceduralHex" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;

        var col = GetComponent<MeshCollider>();
        if (col == null) col = gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
    }
}