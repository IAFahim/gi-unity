using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace EcsDemo
{
    [UpdateAfter(typeof(FloorPushSystem))]
    public partial class FloorMeshSystem : SystemBase
    {
        public static float MaxPush
        {
            get
            {
                var max = 0f;
                for (var i = 0; i < FloorBuffers.CellsPerSide; i++)
                    if (FloorBuffers.RowMax[i] > max) max = FloorBuffers.RowMax[i];
                return max;
            }
        }

        private EntityQuery _cells;
        private bool _built;
        private Mesh _mesh;
        private Material _material;
        private GameObject _floorObject;

        protected override void OnCreate()
        {
            RequireForUpdate<GiField>();
            RequireForUpdate<FloorConfig>();
        }

        protected override void OnDestroy()
        {
            FloorBuffers.Dispose();
            if (_floorObject != null) Object.Destroy(_floorObject);
        }

        protected override void OnUpdate()
        {
            if (!_built)
            {
                BuildFloor(SystemAPI.GetSingleton<FloorConfig>());
                _built = true;
            }

            Dependency.Complete();
            _mesh.SetVertices(FloorBuffers.Positions.Reinterpret<Vector3>(12));
            _mesh.SetNormals(FloorBuffers.Normals.Reinterpret<Vector3>(12));
        }

        private void BuildFloor(FloorConfig config)
        {
            foreach (var filter in Resources.FindObjectsOfTypeAll<MeshFilter>())
            {
                if (filter.gameObject.name == "GiFloor") Object.Destroy(filter.gameObject);
            }

            var n = config.CellsPerSide;
            var step = config.WorldSize / n;

            FloorBuffers.Create(n, config.WorldSize, config.Depth);

            _cells = GetEntityQuery(ComponentType.ReadOnly<FloorCell>());

            var vertices = new Vector3[FloorBuffers.VertCount];
            var uvs = new Vector2[FloorBuffers.VertCount];
            var triangles = new int[n * n * 6];

            for (var z = 0; z <= n; z++)
            for (var x = 0; x <= n; x++)
            {
                vertices[z * (n + 1) + x] = new Vector3(x * step, 0f, z * step);
                uvs[z * (n + 1) + x] = new Vector2(x / (float)n, z / (float)n);
                FloorBuffers.Normals[z * (n + 1) + x] = new float3(0f, 1f, 0f);
            }

            var t = 0;
            for (var z = 0; z < n; z++)
            for (var x = 0; x < n; x++)
            {
                var v0 = z * (n + 1) + x;
                var v1 = v0 + 1;
                var v2 = v0 + n + 1;
                var v3 = v2 + 1;
                triangles[t++] = v0; triangles[t++] = v2; triangles[t++] = v1;
                triangles[t++] = v1; triangles[t++] = v2; triangles[t++] = v3;
            }

            _mesh = new Mesh { name = "GiFloor" };
            _mesh.MarkDynamic();
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _mesh.vertices = vertices;
            _mesh.uv = uvs;
            _mesh.triangles = triangles;
            _mesh.normals = FloorBuffers.Normals.Reinterpret<Vector3>(12).ToArray();
            _mesh.bounds = new Bounds(
                new Vector3(config.WorldSize * 0.5f, -10f, config.WorldSize * 0.5f),
                new Vector3(config.WorldSize, 40f, config.WorldSize));

            for (var i = 0; i < FloorBuffers.VertCount; i++)
            {
                FloorBuffers.Positions[i] = new float3(
                    (i % (n + 1)) * step, 0f, (i / (n + 1)) * step);
            }

            _material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.55f, 0.60f, 0.70f),
            };

            _floorObject = new GameObject("GiFloor");
            _floorObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _floorObject.AddComponent<MeshRenderer>().sharedMaterial = _material;

            var archetype = EntityManager.CreateArchetype(typeof(FloorCell));
            EntityManager.CreateEntity(archetype, FloorBuffers.VertCount, Allocator.Temp);

            var entities = _cells.ToEntityArray(Allocator.Temp);
            var index = 0;
            for (var z = 0; z <= n; z++)
            for (var x = 0; x <= n; x++)
            {
                var cellY = n - z;
                if (cellY > n - 1) cellY = n - 1;
                EntityManager.SetComponentData(entities[index], new FloorCell
                {
                    CellX = x > n - 1 ? n - 1 : x,
                    CellY = cellY,
                    VertIndex = index,
                });
                index++;
            }
            entities.Dispose();
        }
    }
}
