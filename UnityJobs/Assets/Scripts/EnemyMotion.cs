using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

public struct EnemyMotion
{
    public float3 PlayerPosition;
    public float DistanceThreshold;
    public float Step;

    public void Evaluate(float3 position, out float3 nextPosition, out quaternion rotation)
    {
        float2 toPlayer = PlayerPosition.xy - position.xy;
        // At the exact same point, use a fixed direction so the enemy can escape.
        float2 facing = math.normalizesafe(toPlayer, new float2(0f, 1f));
        bool inRange = math.lengthsq(toPlayer) <= DistanceThreshold * DistanceThreshold;
        float2 retreat = math.select(float2.zero, -facing * Step, inRange);
        nextPosition = position + new float3(retreat, 0f);

        // atan2 measures from +X; the arrow sprite points along +Y.
        rotation = quaternion.RotateZ(math.atan2(facing.y, facing.x) - math.PI * 0.5f);
    }
}

[BurstCompile]
public struct MoveEnemiesJob : IJobParallelForTransform
{
    public EnemyMotion Motion;

    public void Execute(int index, TransformAccess transform)
    {
        Motion.Evaluate(transform.position, out float3 position, out quaternion rotation);
        transform.SetPositionAndRotation(position, rotation);
    }
}

[BurstCompile]
public struct MoveEnemyJob : IJob
{
    public EnemyMotion Motion;
    public NativeArray<float3> Position;
    [WriteOnly] public NativeArray<quaternion> Rotation;

    public void Execute()
    {
        // A single job returns this enemy's pose through its native arrays.
        Motion.Evaluate(Position[0], out float3 position, out quaternion rotation);
        Position[0] = position;
        Rotation[0] = rotation;
    }
}
