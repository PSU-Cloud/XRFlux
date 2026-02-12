using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.IO;
using System;

public class LeaderNavigation : MonoBehaviour
{
    private NavMeshAgent agent;

    // Movement settings.
    public float wanderRadius = 100f;
    public float wanderTimer = 3f;
    private float timer;

    // Logging settings.
    [SerializeField]
    private Camera immediateFoVCamera;
    [SerializeField]
    private Camera predictedFoVCamera;
    public float logInterval = 1f;
    private float logTimer;
    private string logFilePath;
    // Dictionary to track FoV state for each object.
    private Dictionary<int, FoVStats> objectStats = new Dictionary<int, FoVStats>();

    // Class to track FoV status for an object.
    private class FoVStats
    {
        public bool immediateInFoV;   // In immediate camera FoV.
        public bool predictedInFoV;   // In predicted camera FoV.
        public string objectName;
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        timer = wanderTimer;

        // Initialize cameras – look for child objects if not set in Inspector.
        if (immediateFoVCamera == null)
        {
            Transform immediateCamTransform = transform.Find("ImmediateFoVCamera");
            if (immediateCamTransform != null)
                immediateFoVCamera = immediateCamTransform.GetComponent<Camera>();
        }
        if (predictedFoVCamera == null)
        {
            Transform predictedCamTransform = transform.Find("PredictedFoVCamera");
            if (predictedCamTransform != null)
                predictedFoVCamera = predictedCamTransform.GetComponent<Camera>();
        }
        if (immediateFoVCamera == null || predictedFoVCamera == null)
        {
            Debug.LogError("One or both camera components not found on " + gameObject.name);
        }
        else
        {
            // Set the FoV values.
            immediateFoVCamera.fieldOfView = 110f;
            predictedFoVCamera.fieldOfView = 130f;
        }

        // Initialize the logging timer.
        logTimer = 0f;
        // Set up logging file in the project root (parent folder of Assets).
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        logFilePath = Path.Combine(projectRoot, "CameraLogs.mylog");
    }

    void Update()
    {
        // Update movement timer and choose a new random position at intervals.
        timer += Time.deltaTime;
        if (timer >= wanderTimer)
        {
            Vector3 newPos = GetRandomNavMeshPosition(transform.position, wanderRadius);
            agent.SetDestination(newPos);
            timer = 0f;
        }

        // Update logging timer and perform FoV check when due.
        logTimer += Time.deltaTime;
        if (logTimer >= logInterval)
        {
            LogObjectsInFoV();
            logTimer = 0f;
        }
    }

    // Returns a random valid position on the NavMesh within the given radius.
    Vector3 GetRandomNavMeshPosition(Vector3 origin, float radius)
    {
        Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * radius;
        randomDirection += origin;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return origin;
    }

    /// <summary>
    /// Checks each renderer in the scene for FoV status (immediate and predicted) and logs any changes.
    /// </summary>
    void LogObjectsInFoV()
    {
        if (immediateFoVCamera == null || predictedFoVCamera == null)
            return;

        // Get view frustum planes for both cameras.
        Plane[] immediatePlanes = GeometryUtility.CalculateFrustumPlanes(immediateFoVCamera);
        Plane[] predictedPlanes = GeometryUtility.CalculateFrustumPlanes(predictedFoVCamera);

        // Find all renderers in the scene.
        Renderer[] renderers = FindObjectsOfType<Renderer>();

        // Keep track of all currently processed object IDs.
        HashSet<int> currentObjectIDs = new HashSet<int>();

        // Use the NavMeshAgent object's name for logging.
        string navAgentName = gameObject.name;
        List<string> eventsToLog = new List<string>();
        string eventTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        foreach (Renderer rend in renderers)
        {
            int objID = rend.gameObject.GetInstanceID();
            string objName = rend.gameObject.name;
            currentObjectIDs.Add(objID);

            // Determine if the renderer is visible in each camera.
            bool immediateVisible = GeometryUtility.TestPlanesAABB(immediatePlanes, rend.bounds);
            bool predictedVisible = GeometryUtility.TestPlanesAABB(predictedPlanes, rend.bounds);

            // Compute distances to the cameras.
            float immediateDistance = Vector3.Distance(immediateFoVCamera.transform.position, rend.transform.position);
            float predictedDistance = Vector3.Distance(predictedFoVCamera.transform.position, rend.transform.position);
            long estimatedSizeBytes = EstimateMeshSize(rend.gameObject);

            if (!objectStats.ContainsKey(objID))
            {
                // New object – log its initial FoV state.
                FoVStats newStats = new FoVStats()
                {
                    immediateInFoV = immediateVisible,
                    predictedInFoV = predictedVisible,
                    objectName = objName
                };
                objectStats.Add(objID, newStats);
                eventsToLog.Add($"{eventTimestamp};;{navAgentName};;{objID};;{objName};;{immediateVisible};;{predictedVisible};;{immediateDistance:F2};;{predictedDistance:F2};;{estimatedSizeBytes}");
            }
            else
            {
                FoVStats stats = objectStats[objID];
                // Log an event if the FoV state has changed.
                if (stats.immediateInFoV != immediateVisible || stats.predictedInFoV != predictedVisible)
                {
                    stats.immediateInFoV = immediateVisible;
                    stats.predictedInFoV = predictedVisible;
                    eventsToLog.Add($"{eventTimestamp};;{navAgentName};;{objID};;{objName};;{immediateVisible};;{predictedVisible};;{immediateDistance:F2};;{predictedDistance:F2};;{estimatedSizeBytes}");
                }
            }
        }

        // For objects no longer present, log an exit event.
        List<int> trackedObjects = new List<int>(objectStats.Keys);
        foreach (int trackedID in trackedObjects)
        {
            if (!currentObjectIDs.Contains(trackedID))
            {
                FoVStats stats = objectStats[trackedID];
                if (stats.immediateInFoV || stats.predictedInFoV)
                {
                    stats.immediateInFoV = false;
                    stats.predictedInFoV = false;
                    eventsToLog.Add($"{eventTimestamp};;{navAgentName};;{trackedID};;{stats.objectName};;False;;False;;N/A;;N/A;;N/A");
                }
            }
        }

        // If there are any events to log, append them to the log file.
        if (eventsToLog.Count > 0)
        {
            string logText = string.Join("\n", eventsToLog);
            File.AppendAllText(logFilePath, logText + "\n");
        }
    }

    /// <summary>
    /// Estimates the size of the object's mesh in bytes assuming 12 bytes per triangle.
    /// </summary>
    private long EstimateMeshSize(GameObject obj)
    {
        MeshFilter mf = obj.GetComponent<MeshFilter>();
        if (mf != null && mf.mesh != null)
        {
            Mesh mesh = mf.mesh;
            int triangleCount = mesh.triangles.Length / 3;
            return triangleCount * 12;
        }
        return 0;
    }
}
