using Gi;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Transforms;

namespace EcsDemo
{
    public partial struct GiFieldBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GiFieldConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<GiFieldConfig>();
            var world = Gi.World.New();
            var grid = Gi.Grid.New(world, config.Power, 0f, 0f, config.WorldSize);
            var layer = Gi.Layer.New(world);
            var stamp = Stamp.Box(config.CubeSpan, config.CubeSpan, (sbyte)config.CubeValue);

            var field = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(field, new GiField
            {
                World = world,
                Grid = grid,
                Layer = layer,
                Stamp = stamp,
                WorldSize = config.WorldSize,
            });

            state.Enabled = false;
        }
    }

    [BurstCompile]
    public partial struct CubeOrbitSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<LocalTransform>()
                .WithAllRW<CubeGiPos>()
                .WithAll<CubeOrbit>()
                .Build(ref state);
            state.RequireForUpdate(_query);
            state.RequireForUpdate<GiField>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var field = SystemAPI.GetSingleton<GiField>();
            state.Dependency = new CubeOrbitJob
            {
                T = (float)SystemAPI.Time.ElapsedTime,
                WorldSize = field.WorldSize,
            }.ScheduleParallel(_query, state.Dependency);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public partial struct CubeOrbitJob : IJobEntity
    {
        public float T;
        public float WorldSize;

        private void Execute(ref LocalTransform transform, in CubeOrbit orbit, ref CubeGiPos giPos)
        {
            var angle = orbit.Phase + T * orbit.Speed;
            var x = orbit.CenterX + math.cos(angle) * orbit.RadiusX;
            var y = orbit.CenterY + math.sin(angle) * orbit.RadiusY;
            giPos.X = x;
            giPos.Y = y;
            transform.Position = new float3(x, orbit.Height, WorldSize - y);
        }
    }

    [UpdateAfter(typeof(CubeOrbitSystem))]
    [BurstCompile]
    public partial struct GiDepositSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GiField>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var field = SystemAPI.GetSingleton<GiField>();
            foreach (var (orbit, giPos, source) in
                     SystemAPI.Query<RefRO<CubeOrbit>, RefRO<CubeGiPos>, RefRW<CubeSource>>())
            {
                if (source.ValueRO.Handle < 0)
                {
                    source.ValueRW.Handle = Gi.World.Place(
                        field.World, field.Layer, giPos.ValueRO.X, giPos.ValueRO.Y,
                        field.Stamp, (int)orbit.ValueRO.Gain);
                }
                else
                {
                    Gi.World.Move(field.World, source.ValueRO.Handle, giPos.ValueRO.X, giPos.ValueRO.Y);
                }
            }

            Gi.World.Process(field.World);
        }
    }

    [UpdateAfter(typeof(GiDepositSystem))]
    [BurstCompile]
    public partial struct FloorPushSystem : ISystem
    {
        private EntityQuery _query;
        private ComponentTypeHandle<FloorCell> _cellHandle;

        public void OnCreate(ref SystemState state)
        {
            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<FloorCell>()
                .Build(ref state);
            _cellHandle = state.GetComponentTypeHandle<FloorCell>(true);
            state.RequireForUpdate(_query);
            state.RequireForUpdate<GiField>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!FloorBuffers.Ready) return;

            var field = SystemAPI.GetSingleton<GiField>();
            _cellHandle.Update(ref state);

            var dependency = new FieldSnapshotJob
            {
                World = field.World,
                Grid = field.Grid,
                Layer = field.Layer,
                Field = FloorBuffers.Field,
                RowMax = FloorBuffers.RowMax,
                CellsPerSide = FloorBuffers.CellsPerSide,
                Depth = FloorBuffers.Depth,
            }.Schedule(FloorBuffers.CellsPerSide, 8, state.Dependency);

            state.Dependency = new DeformJob
            {
                CellHandle = _cellHandle,
                Field = FloorBuffers.Field,
                Positions = FloorBuffers.Positions,
                Normals = FloorBuffers.Normals,
                CellsPerSide = FloorBuffers.CellsPerSide,
                Step = FloorBuffers.Step,
                Depth = FloorBuffers.Depth,
            }.ScheduleParallel(_query, dependency);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct FieldSnapshotJob : IJobParallelFor
    {
        public byte World;
        public byte Grid;
        public byte Layer;
        [WriteOnly][NativeDisableParallelForRestriction] public NativeArray<short> Field;
        [WriteOnly] public NativeArray<float> RowMax;
        public int CellsPerSide;
        public float Depth;

        public unsafe void Execute(int row)
        {
            var dst = (short*)Field.GetUnsafePtr() + row * CellsPerSide;
            Gi.World.QueryRegion(World, Grid, Layer, 0, row, CellsPerSide, 1, dst);

            var max = 0;
            for (var i = 0; i < CellsPerSide; i++)
                if (dst[i] > max) max = dst[i];
            RowMax[row] = max * Depth;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct DeformJob : IJobChunk
    {
        [ReadOnly] public ComponentTypeHandle<FloorCell> CellHandle;
        [ReadOnly][NativeDisableParallelForRestriction] public NativeArray<short> Field;
        [NativeDisableParallelForRestriction] public NativeArray<float3> Positions;
        [WriteOnly][NativeDisableParallelForRestriction] public NativeArray<float3> Normals;
        public int CellsPerSide;
        public float Step;
        public float Depth;

        private int CellX(int gx) => gx < 0 ? 0 : gx > CellsPerSide - 1 ? CellsPerSide - 1 : gx;

        private int CellY(int gz)
        {
            var cy = CellsPerSide - gz;
            return cy < 0 ? 0 : cy > CellsPerSide - 1 ? CellsPerSide - 1 : cy;
        }

        private float Height(int gx, int gz)
            => -Field[CellY(gz) * CellsPerSide + CellX(gx)] * Depth;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var cells = chunk.GetNativeArray(ref CellHandle);
            var row = CellsPerSide + 1;
            for (var i = 0; i < chunk.Count; i++)
            {
                var cell = cells[i];
                var gx = cell.VertIndex % row;
                var gz = cell.VertIndex / row;

                Positions[cell.VertIndex] = new float3(gx * Step, Height(gx, gz), gz * Step);

                var left = Height(gx - 1, gz);
                var right = Height(gx + 1, gz);
                var near = Height(gx, gz - 1);
                var far = Height(gx, gz + 1);
                Normals[cell.VertIndex] = math.normalize(
                    new float3((left - right) / (2f * Step), 1f, (near - far) / (2f * Step)));
            }
        }
    }
}
