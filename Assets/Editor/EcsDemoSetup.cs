using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EcsDemoSetup
{
    private const string SubScenePath = "Assets/Scenes/GiEcsSubScene.unity";
    private const string MeshPath = "Assets/Meshes/CubeMesh.asset";

    public static void FixCubes()
    {
        var mesh = EnsureCubeMesh();
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CubeMat.mat");

        var main = EditorSceneManager.GetActiveScene();
        foreach (var go in main.GetRootGameObjects())
        {
            if (go.name.StartsWith("Cube")) Object.DestroyImmediate(go);
        }
        EditorSceneManager.SaveScene(main);

        var sub = EditorSceneManager.OpenScene(SubScenePath, OpenSceneMode.Additive);
        EditorSceneManager.SetActiveScene(sub);
        try
        {
            foreach (var go in sub.GetRootGameObjects())
            {
                if (go.name.StartsWith("Cube")) Object.DestroyImmediate(go);
            }

            var field = Object.FindFirstObjectByType<GiFieldAuthoring>();
            if (field != null) field.floorDepth = 0.002f;

            const int n = 6;
            const float size = 64f;
            var cell = size / n;
            var k = 0;
            for (var j = 0; j < n; j++)
            for (var i = 0; i < n; i++)
            {
                var go = UnityEditor.ObjectFactory.CreateGameObject("Cube" + k);
                go.transform.position = new Vector3((i + 0.5f) * cell, 0.6f, size - (j + 0.5f) * cell);
                go.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                UnityEditor.ObjectFactory.AddComponent<UnityEngine.MeshFilter>(go).sharedMesh = mesh;
                UnityEditor.ObjectFactory.AddComponent<UnityEngine.MeshRenderer>(go).sharedMaterial = material;
                var a = UnityEditor.ObjectFactory.AddComponent<CubeOriginAuthoring>(go);
                a.centerX = (i + 0.5f) * cell;
                a.centerY = (j + 0.5f) * cell;
                a.radiusX = 3f + (k % 4);
                a.radiusY = 3f + ((k * 3) % 4);
                a.speed = 0.3f + (k % 5) * 0.12f;
                a.phase = k * 0.73f;
                a.gain = 6f + (k % 5) * 2.5f;
                a.height = 0.6f;
                k++;
            }

            EditorSceneManager.SaveScene(sub, SubScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(sub, true);
            EditorSceneManager.SetActiveScene(main);
        }

        Debug.Log("ECSDEMO_FIXCUBES_DONE " + mesh.vertexCount + " verts");
    }

    private static Mesh EnsureCubeMesh()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Meshes"))
            AssetDatabase.CreateFolder("Assets", "Meshes");

        var mesh = new Mesh { name = "CubeMesh" };

        var faceA = new Vector3(0, 0, -1);
        var faceB = new Vector3(0, 0, 1);
        var faceC = new Vector3(-1, 0, 0);
        var faceD = new Vector3(1, 0, 0);
        var faceE = new Vector3(0, 1, 0);
        var faceF = new Vector3(0, -1, 0);

        var vertices = new Vector3[24];
        var normals = new Vector3[24];
        var uvs = new Vector2[24];
        var triangles = new int[36];

        var faces = new[] { faceA, faceB, faceC, faceD, faceE, faceF };
        var v = 0;
        var t = 0;
        foreach (var normal in faces)
        {
            var axis = Mathf.Abs(normal.x) > 0.5f ? Vector3.right : Vector3.up;
            var right = Vector3.Cross(normal, axis).normalized;
            var up = Vector3.Cross(right, normal).normalized;

            var baseIndex = v;
            vertices[v] = (-right - up + normal) * 0.5f;
            vertices[v + 1] = (right - up + normal) * 0.5f;
            vertices[v + 2] = (right + up + normal) * 0.5f;
            vertices[v + 3] = (-right + up + normal) * 0.5f;
            for (var c = 0; c < 4; c++)
            {
                normals[v + c] = normal;
                uvs[v + c] = new Vector2(c == 1 || c == 2 ? 1f : 0f, c >= 2 ? 1f : 0f);
            }

            triangles[t++] = baseIndex;
            triangles[t++] = baseIndex + 2;
            triangles[t++] = baseIndex + 1;
            triangles[t++] = baseIndex;
            triangles[t++] = baseIndex + 3;
            triangles[t++] = baseIndex + 2;
            v += 4;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        AssetDatabase.CreateAsset(mesh, MeshPath);
        AssetDatabase.SaveAssets();
        return mesh;
    }
}

public static class GiPerfProbe
{
    public static unsafe void Run()
    {
        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        var em = world.EntityManager;
        var field = em.CreateEntityQuery(typeof(EcsDemo.GiField))
            .ToComponentDataArray<EcsDemo.GiField>(Unity.Collections.Allocator.Temp)[0];
        var sources = em.CreateEntityQuery(typeof(EcsDemo.CubeSource))
            .ToComponentDataArray<EcsDemo.CubeSource>(Unity.Collections.Allocator.Temp);

        System.GC.Collect();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int frames = 400;
        for (var it = 0; it < frames; it++)
        {
            for (var i = 0; i < sources.Length; i++)
            {
                var dx = System.MathF.Sin(it * 0.01f + i) * 0.1f;
                Gi.World.Move(field.World, sources[i].Handle, 32f + dx, 32f);
            }
            Gi.World.Process(field.World);
        }
        sw.Stop();
        var moveUs = (float)sw.Elapsed.Ticks / frames / 10f;

        var dst = new short[64 * 64];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(
            dst, System.Runtime.InteropServices.GCHandleType.Pinned);
        var p = (short*)handle.AddrOfPinnedObject();
        System.GC.Collect();
        sw.Restart();
        for (var i = 0; i < 2000; i++)
            Gi.World.QueryRegion(field.World, field.Grid, field.Layer, 0, 0, 64, 64, p);
        sw.Stop();
        var regionUs = (float)sw.Elapsed.Ticks / 2000f / 10f;

        sw.Restart();
        for (var i = 0; i < 200_000; i++)
            Gi.World.Query(field.World, field.Grid, field.Layer, i % 64, (i * 7) % 64);
        sw.Stop();
        var cellUs = (float)sw.Elapsed.Ticks / 200_000f / 10f * 1000f;

        handle.Free();
        UnityEngine.Debug.Log($"PERF move+process(36): {moveUs:F1} us | query-region 64x64: {regionUs:F2} us | per-cell query: {cellUs:F3} us");
    }
}
