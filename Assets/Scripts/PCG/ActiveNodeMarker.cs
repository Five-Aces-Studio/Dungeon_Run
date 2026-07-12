using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ActiveNodeMarker : MonoBehaviour
{
    [SerializeField] private Color color = new Color(1.8f, 1.7f, 1.4f);
    [SerializeField, Range(0f, 0.2f)] private float pulseAmplitude = 0.05f;
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField, Range(0.1f, 1f)] private float innerRatio = 0.72f;
    [SerializeField, Range(0.1f, 1.2f)] private float outerRatio = 0.92f;

    private float baseRadius = 1f;

    private void Awake()
    {
        BuildRing();
    }

    private void Update()
    {
        float s = baseRadius * (1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude);
        transform.localScale = new Vector3(s, 1f, s);
    }

    public void Show(Vector3 topCenter, float hexRadius)
    {
        baseRadius = hexRadius;
        transform.position = topCenter + Vector3.up * 0.03f;
        gameObject.SetActive(true);
    }

    private void BuildRing()
    {
        Vector3[] verts = new Vector3[12];
        for (int k = 0; k < 6; k++)
        {
            float a = (60f * k + 30f) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            verts[k] = dir * innerRatio;
            verts[k + 6] = dir * outerRatio;
        }

        int[] tris = new int[36];
        for (int k = 0; k < 6; k++)
        {
            int k1 = (k + 1) % 6;
            int t = k * 6;
            tris[t] = k;     tris[t + 1] = 6 + k1; tris[t + 2] = 6 + k;
            tris[t + 3] = k; tris[t + 4] = k1;     tris[t + 5] = 6 + k1;
        }

        Mesh mesh = new Mesh { name = "ActiveNodeRing" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);

        MeshRenderer rend = GetComponent<MeshRenderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }
}
