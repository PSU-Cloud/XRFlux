using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System.IO;
using System;

public class NavigationScript : MonoBehaviour
{
    private NavMeshAgent agent;

    // Two cameras for immediate and predicted FoV.
    [SerializeField]
    private Camera immediateFoVCamera;
    [SerializeField]
    private Camera predictedFoVCamera;

    public float wanderRadius = 100f;
    public float wanderTimer = 3f; // Time before choosing a new random point
    private float timer;

    // Interval (in seconds) at which we check the FoV.
    public float logInterval = 1f;
    private float logTimer;

    // File path for logging output.
    private string logFilePath;

    // Single dictionary to track FoV state for each object.
    private Dictionary<int, FoVStats> objectStats = new Dictionary<int, FoVStats>();


    // Updated FoV status class with two booleans.
    private class FoVStats
    {
        public bool immediateInFoV; // True if the object is currently in the immediate camera's FoV.
        public bool predictedInFoV; // True if the object is currently in the predicted camera's FoV.
        public String objectName; // The name of the object.
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // Initialize cameras if not assigned in the Inspector.
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
            // Set Field of View for each camera.
            immediateFoVCamera.fieldOfView = 110f;
            predictedFoVCamera.fieldOfView = 130f;
        }

        timer = wanderTimer;
        agent.speed = 100f;
        logTimer = 0f;

        // Use the Unity project root (parent directory of Assets) for logging.
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        logFilePath = Path.Combine(projectRoot, "CameraLogs.mylog");

        // Optionally, clear any previous content and write a header.
        // File.WriteAllText(logFilePath, "Logging started for all cameras.\n");
    }

    void Update()
    {
        timer += Time.deltaTime;
        logTimer += Time.deltaTime;

        if (timer >= wanderTimer)
        {
            Vector3 newPos = GetRandomNavMeshPosition(transform.position, wanderRadius);
            agent.SetDestination(newPos);
            timer = 0f;
        }

        // Check the FoV state every logInterval seconds.
        if (logTimer >= logInterval)
        {
            LogObjectsInFoV();
            logTimer = 0f;
        }
    }

    Vector3 GetRandomNavMeshPosition(Vector3 origin, float radius)
    {
        Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * radius;
        randomDirection += origin;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return origin; // If no valid position found, return current position.
    }

    /// <summary>
    /// Checks every object’s status for both immediate and predicted FoV, and logs an event if there is any change.
    /// Each log entry now includes the NavMeshAgent's object name.
    /// </summary>
    void LogObjectsInFoV()
    {
        if (immediateFoVCamera == null || predictedFoVCamera == null)
            return;

        // Get the view frustum planes for both cameras.
        Plane[] immediatePlanes = GeometryUtility.CalculateFrustumPlanes(immediateFoVCamera);
        Plane[] predictedPlanes = GeometryUtility.CalculateFrustumPlanes(predictedFoVCamera);

        // Find all renderers in the scene.
        Renderer[] renderers = FindObjectsOfType<Renderer>();

        // Keep track of all current object names.
        HashSet<int> currentObjectInt = new HashSet<int>();

        // Get the NavMeshAgent object's name.
        string navAgentName = gameObject.name;

        // List to accumulate event log messages.
        List<string> eventsToLog = new List<string>();
        string eventTimestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        foreach (Renderer rend in renderers)
        {
            int objID = rend.gameObject.GetInstanceID();
            string objName = rend.gameObject.name;
            currentObjectInt.Add(objID);

            // Check FoV for both cameras.
            bool immediateVisible = GeometryUtility.TestPlanesAABB(immediatePlanes, rend.bounds);
            bool predictedVisible = GeometryUtility.TestPlanesAABB(predictedPlanes, rend.bounds);

            // Compute distances from each camera.
            float immediateDistance = Vector3.Distance(immediateFoVCamera.transform.position, rend.transform.position);
            float predictedDistance = Vector3.Distance(predictedFoVCamera.transform.position, rend.transform.position);
            long estimatedSizeBytes = EstimateMeshSize(rend.gameObject);

            if (!objectStats.ContainsKey(objID))
            {
                // New object encountered.
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
                // Check if either FoV status has changed.
                if (stats.immediateInFoV != immediateVisible || stats.predictedInFoV != predictedVisible)
                {
                    stats.immediateInFoV = immediateVisible;
                    stats.predictedInFoV = predictedVisible;
                    eventsToLog.Add($"{eventTimestamp};;{navAgentName};;{objID};;{objName};;{immediateVisible};;{predictedVisible};;{immediateDistance:F2};;{predictedDistance:F2};;{estimatedSizeBytes}");
                }
            }
        }

        // For objects that were previously tracked but are no longer in the scene,
        // assume they are no longer visible and log an exit event.
        List<int> trackedObjects = new List<int>(objectStats.Keys);
        foreach (int trackedObjID in trackedObjects)
        {
            if (!currentObjectInt.Contains(trackedObjID))
            {
                FoVStats stats = objectStats[trackedObjID];

                // Log an event if either FoV state was true.
                if (stats.immediateInFoV || stats.predictedInFoV)
                {
                    stats.immediateInFoV = false;
                    stats.predictedInFoV = false;
                    eventsToLog.Add($"{eventTimestamp};;{navAgentName};;{trackedObjID};;{stats.objectName};;False;;False;;N/A;;N/A;;N/A");
                }
            }
        }

        // Write log entries to file if there are any changes.
        if (eventsToLog.Count > 0)
        {
            string logText = string.Join("\n", eventsToLog);
            File.AppendAllText(logFilePath, logText + "\n");
        }
    }

    /// <summary>
    /// Estimates the size of the object's mesh based on its number of triangles.
    /// Assumes each triangle is stored as 3 indices (4 bytes each) for a total of 12 bytes per triangle.
    /// </summary>
    /// <param name="obj">The GameObject to estimate size for.</param>
    /// <returns>The estimated size in bytes, or 0 if no mesh is found.</returns>
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
