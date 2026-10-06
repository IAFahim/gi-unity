using EcsDemo;
using Unity.Entities;
using UnityEngine;

public class CubeOriginAuthoring : MonoBehaviour
{
    public float centerX;
    public float centerY;
    public float radiusX;
    public float radiusY;
    public float speed = 0.5f;
    public float phase;
    public float gain = 10f;
    public float height = 0.6f;
}

public class CubeOriginBaker : Baker<CubeOriginAuthoring>
{
    public override void Bake(CubeOriginAuthoring authoring)
    {
        var cube = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(cube, new CubeOrbit
        {
            CenterX = authoring.centerX,
            CenterY = authoring.centerY,
            RadiusX = authoring.radiusX,
            RadiusY = authoring.radiusY,
            Speed = authoring.speed,
            Phase = authoring.phase,
            Gain = authoring.gain,
            Height = authoring.height,
        });
        AddComponent(cube, new CubeGiPos());
        AddComponent(cube, new CubeSource { Handle = -1 });
    }
}
