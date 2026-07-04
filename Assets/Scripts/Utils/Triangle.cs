using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralTriangleVisual : MonoBehaviour
{
    public float side = 2f;
    public float thickness = 0.5f;

    private void Awake()
    {
        Build();
    }

    [ContextMenu("Rebuild")]
    public void Build()
    {
        float h = side * 0.8660254f; // sqrt(3)/2
        float x = side * 0.5f;
        float z = h * 0.5f;
        float t = thickness;

        // Planta (vista desde arriba): apex hacia +Z, base en -Z.
        Vector3 A0 = new Vector3(0f, 0f, z);   // apex, cara inferior
        Vector3 B0 = new Vector3(-x, 0f, -z);  // base izq, inferior
        Vector3 C0 = new Vector3(x, 0f, -z);   // base der, inferior
        Vector3 A1 = new Vector3(0f, t, z);    // apex, cara superior
        Vector3 B1 = new Vector3(-x, t, -z);
        Vector3 C1 = new Vector3(x, t, -z);

        // Vértices duplicados por cara para normales planas.
        Vector3[] v =
        {
            // Cara superior (normal +Y)
            A1, C1, B1,
            // Cara inferior (normal -Y)
            A0, B0, C0,
            // Lado izquierdo (A-B)
            A0, A1, B1, B0,
            // Lado derecho (C-A)
            C0, C1, A1, A0,
            // Base (B-C)
            B0, B1, C1, C0
        };

        int[] tris =
        {
            0, 1, 2,        // top
            3, 4, 5,        // bottom
            6, 7, 8,  6, 8, 9,     // lado izq
            10, 11, 12,  10, 12, 13, // lado der
            14, 15, 16,  14, 16, 17  // base
        };

        var mesh = new Mesh { name = "ProceduralTriangle" };
        mesh.vertices = v;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;

        var col = GetComponent<MeshCollider>();
        if (col == null) col = gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
    }
}