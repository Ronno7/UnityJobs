using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public sealed class SwarmReadout : MonoBehaviour
{
    [SerializeField] private Text readout;
    [SerializeField] private EnemySwarm swarm;

    private SwarmMode displayedMode;
    private double windowStart;
    private int frames;
    private bool firstFrame;

    private void OnEnable()
    {
        firstFrame = true;
    }

    private void LateUpdate()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if (firstFrame || displayedMode != swarm.Mode)
        {
            firstFrame = false;
            displayedMode = swarm.Mode;
            windowStart = now;
            frames = 0;
            Display("");
            return;
        }

        frames++;
        double elapsed = now - windowStart;
        if (elapsed < 0.5)
            return;

        double meanSeconds = elapsed / frames;
        Display(string.Format(CultureInfo.InvariantCulture,
            "   {0:F0} FPS ({1:F1} ms)", 1.0 / meanSeconds, meanSeconds * 1000.0));
        windowStart = now;
        frames = 0;
    }

    private void Display(string timing)
    {
        string label = displayedMode switch
        {
            SwarmMode.IndividualUpdates => "Individual updates",
            SwarmMode.ManagerLoop => "Manager loop",
            SwarmMode.ParallelTransformJob => "Parallel transform job",
            SwarmMode.IndividualJobs => "Individual jobs",
            _ => displayedMode.ToString()
        };
        readout.text = label + timing;
    }
}
