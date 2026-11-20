using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Jobs;
using UnityEngine.SceneManagement;

public enum SwarmMode { IndividualUpdates, ManagerLoop, ParallelTransformJob, IndividualJobs }

[DefaultExecutionOrder(-50)]
public sealed class EnemySwarm : MonoBehaviour
{
    [SerializeField] private EnemyAgent enemyPrefab;
    [SerializeField] private Transform player;
    [SerializeField, Min(1)] private int enemyCount = 15000;
    [SerializeField, Min(0.1f)] private float spawnRadius = 8f;
    [SerializeField, Min(0f)] private float distanceThreshold = 2.8f;
    [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
    [SerializeField] private SwarmMode mode = SwarmMode.ParallelTransformJob;

    public SwarmMode Mode => mode;
    public int Count => enemies?.Length ?? 0;
    public EnemyMotion FrameMotion { get; private set; }

    private EnemyAgent[] enemies;
    private Transform[] poses;
    private GameObject[] groups;
    private Vector3 playerStart;
    private TransformAccessArray transformAccess;
    private JobHandle movementHandle;
    private NativeArray<float3> singlePosition;
    private NativeArray<quaternion> singleRotation;
    private bool individualUpdatesEnabled;

    private void Start()
    {
        playerStart = player.position;
        enemies = new EnemyAgent[enemyCount];
        poses = new Transform[enemyCount];

        // Unity processes writable transforms that share a hierarchy on one worker.
        // Independent root groups let the parallel job divide the swarm among workers.
        const int groupSize = 128;
        groups = new GameObject[(enemyCount + groupSize - 1) / groupSize];
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i] = new GameObject($"Enemy group {i + 1}");
            SceneManager.MoveGameObjectToScene(groups[i], gameObject.scene);
        }
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyAgent enemy = Instantiate(enemyPrefab, groups[i / groupSize].transform);
            enemy.Initialize(this);
            enemies[i] = enemy;
            poses[i] = enemy.transform;
        }

        ResetPositions();
        transformAccess = new TransformAccessArray(poses);
        // Each single-enemy job finishes before the next one reuses these buffers.
        singlePosition = new NativeArray<float3>(1, Allocator.Persistent);
        singleRotation = new NativeArray<quaternion>(1, Allocator.Persistent);
    }

    private void ResetPositions()
    {
        player.position = playerStart;
        var random = new Unity.Mathematics.Random(41717);
        for (int i = 0; i < poses.Length; i++)
        {
            float angle = random.NextFloat(0f, 2f * math.PI);
            float radius = spawnRadius * math.sqrt(random.NextFloat());
            math.sincos(angle, out float sine, out float cosine);
            poses[i].position = transform.position + new Vector3(cosine * radius, sine * radius, 0f);
        }
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) mode = SwarmMode.IndividualUpdates;
            if (keyboard.digit2Key.wasPressedThisFrame) mode = SwarmMode.ManagerLoop;
            if (keyboard.digit3Key.wasPressedThisFrame) mode = SwarmMode.ParallelTransformJob;
            if (keyboard.digit4Key.wasPressedThisFrame) mode = SwarmMode.IndividualJobs;
            if (keyboard.rKey.wasPressedThisFrame) ResetPositions();
        }

        SetIndividualUpdates(mode == SwarmMode.IndividualUpdates);
        FrameMotion = new EnemyMotion {
            PlayerPosition = player.position,
            DistanceThreshold = distanceThreshold,
            Step = moveSpeed * Time.deltaTime
        };

        switch (mode)
        {
            case SwarmMode.ManagerLoop:
                foreach (Transform pose in poses) MoveOnMainThread(pose, FrameMotion);
                break;
            case SwarmMode.ParallelTransformJob:
                var job = new MoveEnemiesJob { Motion = FrameMotion };
                movementHandle = job.Schedule(transformAccess);
                break;
            case SwarmMode.IndividualJobs:
                foreach (Transform pose in poses) MoveWithSingleJob(pose);
                break;
        }
    }

    public static void MoveOnMainThread(Transform pose, EnemyMotion motion)
    {
        motion.Evaluate(pose.position, out float3 position, out quaternion rotation);
        pose.SetPositionAndRotation(position, rotation);
    }

    private void MoveWithSingleJob(Transform pose)
    {
        singlePosition[0] = pose.position;
        var job = new MoveEnemyJob {
            Motion = FrameMotion,
            Position = singlePosition,
            Rotation = singleRotation
        };
        job.Schedule().Complete();
        pose.SetPositionAndRotation(singlePosition[0], singleRotation[0]);
    }

    private void SetIndividualUpdates(bool active)
    {
        if (individualUpdatesEnabled == active) return;
        individualUpdatesEnabled = active;
        foreach (EnemyAgent enemy in enemies)
        {
            // Scene unload can destroy another root's agents before this manager.
            if (enemy != null) enemy.enabled = active;
        }
    }

    private void LateUpdate()
    {
        movementHandle.Complete();
    }

    private void OnDisable()
    {
        movementHandle.Complete();
        SetIndividualUpdates(false);
    }

    private void OnDestroy()
    {
        if (transformAccess.isCreated) transformAccess.Dispose();
        if (singlePosition.IsCreated) singlePosition.Dispose();
        if (singleRotation.IsCreated) singleRotation.Dispose();
        if (groups == null) return;
        foreach (GameObject group in groups)
            if (group != null) Destroy(group);
    }
}
