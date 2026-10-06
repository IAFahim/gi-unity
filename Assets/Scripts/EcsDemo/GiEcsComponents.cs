using Unity.Collections;
using Unity.Entities;

namespace EcsDemo
{
    public struct GiFieldConfig : IComponentData
    {
        public int Power;
        public float WorldSize;
        public int CubeSpan;
        public int CubeValue;
    }

    public struct GiField : IComponentData
    {
        public byte World;
        public byte Grid;
        public byte Layer;
        public byte Stamp;
        public float WorldSize;
    }

    public struct CubeOrbit : IComponentData
    {
        public float CenterX;
        public float CenterY;
        public float RadiusX;
        public float RadiusY;
        public float Speed;
        public float Phase;
        public float Gain;
        public float Height;
    }

    public struct CubeGiPos : IComponentData
    {
        public float X;
        public float Y;
    }

    public struct CubeSource : IComponentData
    {
        public int Handle;
    }

    public struct FloorConfig : IComponentData
    {
        public int CellsPerSide;
        public float WorldSize;
        public float Depth;
    }

    public struct FloorCell : IComponentData
    {
        public int CellX;
        public int CellY;
        public int VertIndex;
    }

    public static class FloorBuffers
    {
        public static NativeArray<short> Field;
        public static NativeArray<float> RowMax;
        public static NativeArray<Unity.Mathematics.float3> Positions;
        public static NativeArray<Unity.Mathematics.float3> Normals;
        public static int CellsPerSide;
        public static int VertCount;
        public static float Step;
        public static float Depth;

        public static bool Ready => Positions.IsCreated;

        public static void Create(int cellsPerSide, float worldSize, float depth)
        {
            Dispose();
            CellsPerSide = cellsPerSide;
            VertCount = (cellsPerSide + 1) * (cellsPerSide + 1);
            Step = worldSize / cellsPerSide;
            Depth = depth;
            Field = new NativeArray<short>(cellsPerSide * cellsPerSide, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            RowMax = new NativeArray<float>(cellsPerSide, Allocator.Persistent);
            Positions = new NativeArray<Unity.Mathematics.float3>(VertCount, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            Normals = new NativeArray<Unity.Mathematics.float3>(VertCount, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
        }

        public static void Dispose()
        {
            if (Field.IsCreated) Field.Dispose();
            if (RowMax.IsCreated) RowMax.Dispose();
            if (Positions.IsCreated) Positions.Dispose();
            if (Normals.IsCreated) Normals.Dispose();
        }
    }
}
