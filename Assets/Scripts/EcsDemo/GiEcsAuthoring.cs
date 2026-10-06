using EcsDemo;
using Unity.Entities;
using UnityEngine;

public class GiFieldAuthoring : MonoBehaviour
{
    public int gridPower = 6;
    public float worldSize = 64f;
    public int cubeSpan = 10;
    public int cubeValue = 70;
    public float floorDepth = 0.012f;
}

public class GiFieldBaker : Baker<GiFieldAuthoring>
{
    public override void Bake(GiFieldAuthoring authoring)
    {
        var config = GetEntity(TransformUsageFlags.None);
        AddComponent(config, new GiFieldConfig
        {
            Power = authoring.gridPower,
            WorldSize = authoring.worldSize,
            CubeSpan = authoring.cubeSpan,
            CubeValue = authoring.cubeValue,
        });
        AddComponent(config, new FloorConfig
        {
            CellsPerSide = 1 << authoring.gridPower,
            WorldSize = authoring.worldSize,
            Depth = authoring.floorDepth,
        });
    }
}
