using UnityEngine;

public sealed class EnemyAgent : MonoBehaviour
{
    private EnemySwarm swarm;

    public void Initialize(EnemySwarm owner)
    {
        swarm = owner;
        enabled = false;
    }

    // Only the individual Update comparison enables this component.
    private void Update()
    {
        EnemySwarm.MoveOnMainThread(transform, swarm.FrameMotion);
    }
}
