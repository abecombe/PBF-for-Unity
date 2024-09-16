using UnityEngine;

public class BillboardQuadBuilder
{
    public Mesh Mesh { get; private set; }

    public Mesh UpdatedMesh
    {
        get
        {
            UpdateVertices();
            return Mesh;
        }
    }

    private  Camera _camera;

    private bool _inited = false;

    private static readonly Vector3[] FaceVertices =
    {
        new(-1f, 1f, 0),
        new(1f, 1f, 0),
        new(1f, -1f, 0),
        new(-1f, -1f, 0)
    };

    private Vector3[] _vertices = new Vector3[4];

    public void Init(Camera camera)
    {
        _inited = true;

        _camera = camera;

        Mesh = new Mesh
        {
            name = "BillboardQuad"
        };

        UpdateVertices();
        var triangles = new[]
        {
            0, 1, 2,
            2, 3, 0,
        };
        var uvs = new[]
        {
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0),
            new Vector2(0, 0),
        };
        Mesh.SetVertices(_vertices);
        Mesh.SetTriangles(triangles, 0);
        Mesh.SetUVs(0, uvs);
    }

    public void UpdateVertices()
    {
        if (!_inited)
        {
            Debug.LogError("BillboardQuadBuilder is not initialized.");
            return;
        }

        var rotation = Quaternion.LookRotation(_camera.transform.forward, _camera.transform.up);
        for (var i = 0; i < _vertices.Length; i++)
        {
            _vertices[i] = rotation * FaceVertices[i];
        }

        Mesh.SetVertices(_vertices);
    }
}