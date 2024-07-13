using System.Linq;
using UnityEngine;

[System.Serializable]
public struct Vertex
{
    public Vector3 position;
    public Vector3 normal;
    public Vector2 uv;
}

[System.Serializable]
public struct Triangle
{
    public int subIndex;
    public int a;
    public int b; 
    public int c;

    public readonly int[] Array => new int[] { a, b, c };
}

[System.Serializable]
public struct HeightRenderer
{
    public Material Material;
    [Range(0f, 1f)] public float LimitHeight;
}

[System.Serializable]
public struct PrismMeshRendererSettings
{
    public Material BaseMaterial;
    public Material TopMaterial;
    public HeightRenderer[] HeightsRenderer;
}

[System.Serializable]
public class PrismMeshData
{
    public const int MinPolyCount = 3;

    private int m_PolyCount;
    private Vector3[] m_PolySummits;
    private Vector2 m_TopOffset;
    private Vector2Int m_Sampling;
    private float m_Slope;

    private Vertex[] vertices = new Vertex[0];
    private Triangle[][] triangles = new Triangle[0][];

    public Vector3[] PolySummits
    {
        get => m_PolySummits; 
        set
        {
            if (value == null)
            {
                Debug.LogError($"PolySummits Array can not be null");
                return;
            }

            m_PolyCount = Mathf.Max(MinPolyCount, value.Length);
            m_PolySummits = new Vector3[m_PolyCount].Select((_, index) =>
            {
                return index < value.Length ? value[index] : new Vector3(1f, 1f, 1f);
            }).ToArray();
        }
    }

    public Vector2 TopOffset
    {
        get => m_TopOffset;
        set => m_TopOffset = value;
    }

    public Vector2Int Sampling
    {
        get => m_Sampling; 
        set => m_Sampling = new()
        {
            x = Mathf.Clamp(value.x, 1, int.MaxValue),
            y = Mathf.Clamp(value.y, 1, int.MaxValue),
        };
    }

    public float Slope
    {
        get => m_Slope; 
        set => m_Slope = Mathf.Clamp(value, -1f, 1f);
    }

    public int PolyCount => m_PolyCount;

    public int BaseCount => BaseVerticesCount(PolyCount, Sampling);

    public int SideCount => SideVerticesCount(PolyCount, Sampling);

    public int VerticesCount => MeshVerticesCount(PolyCount, Sampling);

    public Vector3[] Vertices => vertices.Select(vertex => vertex.position).ToArray();

    public Vector3[] Normals => vertices.Select(vertex => vertex.normal).ToArray();

    public Vector2[] UVs => vertices.Select(vertex => vertex.uv).ToArray();

    public int[][] Triangles => triangles.Select(subTriangles =>
    {
        int[] subTriangleArray = new int[0];
        for (int index = 0; index < subTriangles.Length; index++)
        {
            subTriangleArray = subTriangleArray.Concat(subTriangles[index].Array).ToArray();
        }

        return subTriangleArray;
    }).ToArray();

    public PrismMeshData()
    {
        PolySummits = RegularPrismSummits(MinPolyCount, 1f, false, 0f, 1f);
        TopOffset = Vector2.zero;
        Sampling = new(1, 1);
        Slope = 0f;
    }

    public PrismMeshData(Vector3[] polySummits, Vector2 topOffset, Vector2Int sampling, float slope)
    {
        PolySummits = polySummits;
        TopOffset = topOffset;
        Sampling = sampling;
        Slope = slope;
    }

    public static Vector3[] RegularPrismSummits(float size, bool isPentoseFloret, float angleOffset, float[] polyHeights)
    {
        polyHeights ??= new float[0];
        int polyCount = Mathf.Max(MinPolyCount, polyHeights.Length);

        float floretFactor = 1f - (1f / Mathf.Sqrt(13f - 4 * Mathf.Sqrt(polyCount / 2f) * Mathf.Cos(Mathf.PI / polyCount))) * (1f + 2 * Mathf.Sin(Mathf.PI / polyCount));

        polyCount *= isPentoseFloret ? 3 : 1;

        return new int[polyCount].Select((_, polyIndex) =>
        {
            float angle = angleOffset + polyIndex * 2f * Mathf.PI / polyCount;
            float factor = 1f - (isPentoseFloret && (polyIndex % 3) == 0 ? floretFactor : 0f);

            return new Vector3()
            {
                x = factor * size * Mathf.Cos(angle),
                y = polyIndex < polyHeights.Length ? polyHeights[polyIndex] : 1f,
                z = factor * size * Mathf.Sin(angle),
            };
        }).ToArray();
    }

    public static Vector3[] RegularPrismSummits(int polyCount, float size, bool isPentoseFloret, float angleOffset, float height)
    {
        float[] polyHeights = new float[polyCount].Select(_ => height).ToArray();
        return RegularPrismSummits(size, isPentoseFloret, angleOffset, polyHeights);
    }

    public static Vector3[] SingleHeightSummits(float height, Vector3[] polySummits)
    {
        return polySummits.Select(summit => new Vector3(summit.x, Mathf.Max(0f, height), summit.z)).ToArray();
    }

    public void RecalculateMeshData()
    {
        SetVertices();
        SetTriangles();
    }

    public static int BaseVerticesCount(int polyCount, Vector2Int sampling)
    {
        polyCount = Mathf.Max(MinPolyCount, polyCount);
        sampling = new()
        {
            x = Mathf.Clamp(sampling.x, 1, int.MaxValue),
            y = Mathf.Clamp(sampling.y, 1, int.MaxValue),
        };

        return 1 + Mathf.RoundToInt(sampling.x * (sampling.x + 1) * (polyCount / 2f));
    }

    public static int BaseTrianglesCount(int polyCount, Vector2Int sampling)
    {
        polyCount = Mathf.Max(MinPolyCount, polyCount);
        sampling = new()
        {
            x = Mathf.Clamp(sampling.x, 1, int.MaxValue),
            y = Mathf.Clamp(sampling.y, 1, int.MaxValue),
        };

        return polyCount * (1 + sampling.x * (sampling.x + 1));
    }

    public static int SideVerticesCount(int polyCount, Vector2Int sampling)
    {
        polyCount = Mathf.Max(MinPolyCount, polyCount);
        sampling = new()
        {
            x = Mathf.Clamp(sampling.x, 1, int.MaxValue),
            y = Mathf.Clamp(sampling.y, 1, int.MaxValue),
        };

        return (sampling.y - 1) * sampling.x * polyCount;
    }

    public static int SideTrianglesCount(int polyCount, Vector2Int sampling)
    {
        polyCount = Mathf.Max(MinPolyCount, polyCount);
        sampling = new()
        {
            x = Mathf.Clamp(sampling.x, 1, int.MaxValue),
            y = Mathf.Clamp(sampling.y, 1, int.MaxValue),
        };

        return sampling.y * 2 * sampling.x * polyCount;
    }

    public static int MeshVerticesCount(int polyCount, Vector2Int sampling)
    {
        return 2 * BaseVerticesCount(polyCount, sampling) + SideVerticesCount(polyCount, sampling); 
    }

    public static int MeshTrianglesCount(int polyCount, Vector2Int sampling)
    {
        return 2 * BaseTrianglesCount(polyCount, sampling) + SideTrianglesCount(polyCount, sampling);
    }

    public int VertexIndex(int x, int y, int z)
    {
        y = Mathf.Clamp(y, 0, Sampling.y);
        z = y == 0 || y == Sampling.y ? Mathf.Clamp(z, 0, Sampling.x) : Sampling.x;
        x = z == 0 ? 0 : (Mathf.Abs(x) % (z * PolyCount));

        int b = y > 0 ? BaseCount : Mathf.RoundToInt(z * (z - 1) * PolyCount / 2f) + x + Mathf.Min(z, 1);
        int m = y == 0 || Sampling.y < 2 ? 0 : y == Sampling.y ? SideCount : Mathf.Max(y - 1, 0) * z * PolyCount + x;
        int t = y < Sampling.y ? 0 : Mathf.RoundToInt(z * (z - 1) * PolyCount / 2f) + x + Mathf.Min(z, 1);

        return b + m + t;
    }

    private float Lerp3(float a, float b, float c, float t)
    {
        t = Mathf.Clamp(t, -1f, 1f);

        return t < 0f ? Mathf.Lerp(a, b, t + 1f) : Mathf.Lerp(b, c, t);
    }

    private Vector3 Bezier(Vector3 start, Vector3 control, Vector3 end, float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);

        return (1f - t) * (1f - t) * start + 2f * (1f - t) * t * control + t * t * end;
    }

    private void SetVertices()
    {
        vertices = new Vertex[VerticesCount];

        float[] polyHeights = PolySummits.Select(summit => summit.y).ToArray();
        float centerHeight = Lerp3(polyHeights.Min(), polyHeights.Sum() / polyHeights.Length, polyHeights.Max(), Slope);

        for (int y = 0; y <= Sampling.y; y++)
        {
            int zMin = y == 0 || y == Sampling.y ? 0 : Sampling.x;
            float yRatio = (float)y / Sampling.y;

            for (int z = zMin; z <= Sampling.x; z++)
            {
                float zRatio = (float)z / Sampling.x;

                if (z == 0)
                {
                    float height = yRatio * centerHeight;
                    int index = VertexIndex(0, y, z);
                    vertices[index].position = new(0f, height, 0f);
                }
                else
                {
                    for (int x = 0; x < z * PolyCount; x++)
                    {
                        int polyIndex = x / z;
                        int nextPolyIndex = (polyIndex + 1) % PolyCount;
                        float xRatio = (float)(x - z * polyIndex) / z;

                        float height = yRatio * Mathf.Lerp(Mathf.Lerp(polyHeights[polyIndex], polyHeights[nextPolyIndex], xRatio), centerHeight, 1 - zRatio);
                        Vector3 vertexPos = zRatio * Vector3.Lerp(PolySummits[polyIndex], PolySummits[nextPolyIndex], xRatio);

                        /*if (y == Sampling.y && z < Sampling.x) 
                        {
                            vertexPos.y = Lerp3(polyHeights.Min(), height, polyHeights.Max(), Slope);
                            height = Bezier(vertexPos / zRatio, vertexPos, new(0f, centerHeight, 0f), 1 - zRatio).y;
                        }*/

                        int index = VertexIndex(x, y, z);
                        vertices[index].position = new(vertexPos.x, height, vertexPos.z);
                    }
                }
            }
        }
    }

    private void SetTriangles()
    {
        triangles = new Triangle[][] { new Triangle[0], new Triangle[0], new Triangle[0] }; 

        for (int z = 1; z <= Sampling.x; z++)
        {
            for (int x = 0; x < z * PolyCount; x++)
            {
                int nx = (x + 1) % (z * PolyCount);

                int dz = z - 1;
                int pi = x / z;
                int npi = (pi + 1) % PolyCount;
                int s = x - z * pi;
                int ns = (s + 1) % z;

                int r = pi * dz + s;
                int nr = npi * dz + ns;

                triangles[0] = triangles[0].Append(new Triangle()
                {
                    a = VertexIndex(s < dz ? r : nr, 0, dz),
                    b = VertexIndex(x, 0, z),
                    c = VertexIndex(nx, 0, z),
                }).ToArray();

                triangles[2] = triangles[2].Append(new Triangle()
                {
                    a = VertexIndex(x, Sampling.y, z),
                    b = VertexIndex(s < dz ? r : nr, Sampling.y, dz),
                    c = VertexIndex(nx, Sampling.y, z),
                }).ToArray();

                if (s < dz)
                {
                    int dns = (s + 1) % dz;
                    int dr = pi * dz + dns;
                    int dnr = npi * dz + dns;

                    triangles[0] = triangles[0].Append(new Triangle()
                    {
                        a = VertexIndex(s + 1 < dz ? dr : dnr, 0, dz),
                        b = VertexIndex(pi * dz + s, 0, dz),
                        c = VertexIndex(pi * z + ns, 0, z),
                    }).ToArray();

                    triangles[2] = triangles[2].Append(new Triangle()
                    {
                        a = VertexIndex(pi * dz + s, Sampling.y, dz),
                        b = VertexIndex(s + 1 < dz ? dr : dnr, Sampling.y, dz),
                        c = VertexIndex(pi * z + ns, Sampling.y, z),
                    }).ToArray();
                }
            }   
        }

        for (int y = 1; y <= Sampling.y; y++)
        {
            for (int x = 0; x < Sampling.x * PolyCount; x++)
            {
                int dy = y - 1;
                int nx = (x + 1) % (Sampling.x * PolyCount);

                triangles[1] = triangles[1].Append(new Triangle()
                {
                    a = VertexIndex(x, dy, Sampling.x),
                    b = VertexIndex(x, y, Sampling.x),
                    c = VertexIndex(nx, y, Sampling.x),
                }).ToArray();

                triangles[1] = triangles[1].Append(new Triangle()
                {
                    a = VertexIndex(x, dy, Sampling.x),
                    b = VertexIndex(nx, y, Sampling.x),
                    c = VertexIndex(nx, dy, Sampling.x),
                }).ToArray();
            }
        }
    }
}
