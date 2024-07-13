using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class PrismNode : MonoBehaviour
{
    public static Vector3Int ThreadSize => new(8, 4, 2);

    [Header("Prism Settings")]
    public Vector3[] polySummits = new Vector3[0];
    public Vector2 topOffset = Vector2.zero;
    [Min(1)] public Vector2Int sampling = Vector2Int.one;
    [Range(-1f, 1f)] public float slope = 0f;

    [Header("Regular Prism Settings")]
    public bool isRegular = false;
    [Min(0f)] public float size = 1f;
    public bool isPentoseFloret = false;
    public float angleOffset = 0f;
    [Min(0f)] public float[] polyHeights = new float[0];

    [Header("Single Height Settings")]
    public bool useSingleHeight = false;
    [Min(0f)] public float height = 1f;
    [Min(PrismMeshData.MinPolyCount)] public int polyCount = PrismMeshData.MinPolyCount;

    [Header("Compute Shader Settings")]
    public bool useComputeShader;
    [SerializeField] private ComputeShader computeShader;

    [Header("Renderer Settings")]
    public bool rendMesh;

    [Header("Debug Settings")]
    [SerializeField] private bool debug = false;
    [SerializeField, Min(0f)] private float vertexRadius = 1f;
    [SerializeField] private Color vertexColor = Color.black;
    [SerializeField] private Color triangleColor = Color.white;

    private PrismMeshData meshData = new();
    private Vector3[] vertices = new Vector3[0];
    private int[][] triangles = new int[0][];

    private ComputeBuffer summitsBuffer;
    private ComputeBuffer verticesBuffer;
    private ComputeBuffer trianglesBuffer;

    private void OnValidate()
    {
        RecalculateProperties();

        if (useComputeShader)
        {
            //RecalculateWithComputeShader();
        }
        else { RecalculateWithMeshData(); }

        GetComponent<MeshFilter>().sharedMesh = null;
        if (rendMesh)
        {
            RendMesh();
        }
    }

    public void RecalculateProperties()
    {
        float angleOffset = this.angleOffset * Mathf.Deg2Rad;

        if (useSingleHeight)
        {
            if (isRegular)
            {
                polySummits = PrismMeshData.RegularPrismSummits(polyCount, size, isPentoseFloret, angleOffset, height);
            }

            polySummits = PrismMeshData.SingleHeightSummits(height, polySummits);
        }
        else if (isRegular)
        {
            polySummits = PrismMeshData.RegularPrismSummits(size, isPentoseFloret, angleOffset, polyHeights);
        }

        polyHeights = polySummits.Select(summit => summit.y).ToArray();
        polyCount = polySummits.Length;
    }

    public void RendMesh()
    {
        Mesh mesh = new()
        {
            vertices = vertices,
            uv = meshData.UVs,
            subMeshCount = triangles.Length,
        };

        for (int index = 0; index < triangles.Length; index++)
        {
            mesh.SetTriangles(triangles[index], index);
        }

        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    public void RecalculateWithMeshData()
    {
        meshData = new PrismMeshData(polySummits, topOffset, sampling, slope);

        meshData.RecalculateMeshData();
        vertices = meshData.Vertices;
        triangles = meshData.Triangles;
    }

    public void RecalculateWithComputeShader()
    {
        if (computeShader == null)
        {
            Debug.LogError("Compute Shader can not be null");
            return;
        }

        if (summitsBuffer == null || summitsBuffer.count != polySummits.Length)
        {
            summitsBuffer?.Release();
            summitsBuffer = new ComputeBuffer(polySummits.Length, 3 * sizeof(float));
        }

        int verticesCount = PrismMeshData.MeshVerticesCount(polySummits.Length, sampling);
        Vertex[] vertices = new Vertex[verticesCount];
        if (verticesBuffer == null || verticesBuffer.count != verticesCount)
        {
            verticesBuffer?.Release();
            verticesBuffer = new ComputeBuffer(verticesCount, 8 * sizeof(float));
        }

        int trianglesCount = PrismMeshData.MeshTrianglesCount(polySummits.Length, sampling);
        Triangle[] triangles = new Triangle[trianglesCount];
        if (trianglesBuffer == null || trianglesBuffer.count != trianglesCount)
        {
            trianglesBuffer?.Release();
            trianglesBuffer = new(trianglesCount, 4 * sizeof(int), ComputeBufferType.Append);
        }

        int kernelIndex = computeShader.FindKernel("BuildPrismMesh");

        computeShader.SetBuffer(kernelIndex, "polySummits", summitsBuffer);
        computeShader.SetBuffer(kernelIndex, "vertices", verticesBuffer);
        computeShader.SetBuffer(kernelIndex, "triangles", trianglesBuffer);

        computeShader.SetVector("topOffset", topOffset);
        computeShader.SetBool("isPentoseFloret", isPentoseFloret);
        computeShader.SetVector("sampling", new Vector2(sampling.x, sampling.y));
        computeShader.SetFloat("slope", slope);

        trianglesBuffer.SetCounterValue(0);
        summitsBuffer.SetData(polySummits);
        computeShader.Dispatch(kernelIndex, 1 + sampling.x * polySummits.Length / ThreadSize.x, 1 + sampling.y / ThreadSize.y, 1 + sampling.x / ThreadSize.z);
        verticesBuffer.GetData(vertices);
        trianglesBuffer.GetData(triangles);

        this.vertices = vertices.Select(vertex => vertex.position).ToArray();

        /*int subMeshCount = 1 + triangles.Max(triangle => triangle.subIndex);
        this.triangles = new int[subMeshCount][];
        for (int index = 0; index < triangles.Length; index++)
        {
            Debug.Log($"{this.triangles.Length} - {triangles[index].subIndex}");
            this.triangles[triangles[index].subIndex] ??= new int[0];
            this.triangles[triangles[index].subIndex] = this.triangles[triangles[index].subIndex].Concat(triangles[index].Array).ToArray();
        }*/
    }

    private void OnDrawGizmos()
    {
        if (debug)
        {
            Gizmos.color = vertexColor;
            foreach (Vector3 vertex in vertices)
            {
                Gizmos.DrawSphere(transform.position + vertex, vertexRadius);
            }

            Gizmos.color = triangleColor;
            foreach (int[] subTriangles in triangles)
            {
                for (int index = 2; index < subTriangles.Length; index += 3)
                {
                    Vector3 point1 = transform.position + vertices[subTriangles[index]];
                    Vector3 point2 = transform.position + vertices[subTriangles[index - 1]];
                    Vector3 point3 = transform.position + vertices[subTriangles[index - 2]];

                    Gizmos.DrawLine(point1, point2);
                    Gizmos.DrawLine(point2, point3);
                    Gizmos.DrawLine(point3, point1);
                }
            }
        }
    }
}
