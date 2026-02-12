using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.IO;
using System;

public class FollowerNavigation : MonoBehaviour 
{
    private NavMeshAgent agent;
    // Following behavior settings.
    public float updateInterval = 2f;
    private float updateTimer = 0f;
    // Desired radius around the leader within which the follower should stay.
    public float followRadius = 5f;

    // Logging settings.
    [SerializeField]
    private Camera immediateFoVCamera;
    [SerializeField]
    private Camera predictedFoVCamera;
    public float logInterval = 1f;
    private float logTimer;
    private string logFilePath;
    private Dictionary<int, FoVStats> objectStats = new Dictionary<int, FoVStats>();

    private class FoVStats
    {
        public bool immediateInFoV;   // True if object is currently in immediate FoV.
        public bool predictedInFoV;   // True if object is in predicted FoV.
        public string objectName;
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        updateTimer = updateInterval; // Start with immediate update.
        logTimer = 0f;

        // Initialize cameras – try to locate them as child objects if not set in Inspector.
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
            immediateFoVCamera.fieldOfView = 110f;
            predictedFoVCamera.fieldOfView = 130f;
        }

        // Set up the log file in the project root.
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        logFilePath = Path.Combine(projectRoot, "CameraLogs.mylog");
    }

    void Update()
    {
        // Following behavior: update destination to follow the closest leader.
        updateTimer += Time.deltaTime;
        if (updateTimer >= updateInterval)
        {
            GameObject closestLeader = FindClosestLeader();
            if (closestLeader != null)
            {
                Vector3 targetPos = GetRandomPositionNearLeader(closestLeader.transform.position, followRadius);
                agent.SetDestination(targetPos);
            }
            updateTimer = 0f;
        }

        // Update logging timer and check FoV states.
        logTimer += Time.deltaTime;
        if (logTimer >= logInterval)
        {
            LogObjectsInFoV();
            logTimer = 0f;
        }
    }

    // Searches for the closest leader (object with LeaderNavigation component).
    GameObject FindClosestLeader()
    {
        LeaderNavigation[] leaders = FindObjectsOfType<LeaderNavigation>();
        if (leaders.Length == 0)
        {
            return null;
        }

        GameObject closestLeader = leaders[0].gameObject;
        float minDistance = Vector3.Distance(transform.position, leaders[0].transform.position);

        for (int i = 1; i < leaders.Length; i++)
        {
            float dist = Vector3.Distance(transform.position, leaders[i].transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestLeader = leaders[i].gameObject;
            }
        }
        return closestLeader;
    }

    // Returns a random position near the leader within the specified radius.
    Vector3 GetRandomPositionNearLeader(Vector3 leaderPosition, float radius)
    {
        Vector3 offset = new Vector3(UnityEngine.Random.Range(-radius, radius), 0f, UnityEngine.Random.Range(-radius, radius));
        return leaderPosition + offset;
    }

    /// <summary>
    /// Checks each renderer in the scene for changes in immediate and predicted FoV status and logs any updates.
    /// </summary>
    void LogObjectsInFoV()
    {
        if (immediateFoVCamera == null || predictedFoVCamera == null)
            return;

        Plane[] immediatePlanes = GeometryUtility.CalculateFrustumPlanes(immediateFoVCamera);
        Plane[] predictedPlanes = GeometryUtility.CalculateFrustumPlanes(predictedFoVCamera);

        Renderer[] renderers = FindObjectsOfType<Renderer>();
        HashSet<int> currentObjectIDs = new HashSet<int>();

        string navAgentName = gameObject.name;
        List<string> eventsToLog = new List<string>();
        string eventTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        foreach (Renderer rend in renderers)
        {
            int objID = rend.gameObject.GetInstanceID();
            string objName = rend.gameObject.name;
            currentObjectIDs.Add(objID);

            bool immediateVisible = GeometryUtility.TestPlanesAABB(immediatePlanes, rend.bounds);
            bool predictedVisible = GeometryUtility.TestPlanesAABB(predictedPlanes, rend.bounds);

            float immediateDistance = Vector3.Distance(immediateFoVCamera.transform.position, rend.transform.position);
            float predictedDistance = Vector3.Distance(predictedFoVCamera.transform.position, rend.transform.position);
            long estimatedSizeBytes = EstimateMeshSize(rend.gameObject);

            if (!objectStats.ContainsKey(objID))
            {
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
                if (stats.immediateInFoV != immediateVisible || stats.predictedInFoV != predictedVisible)
                {
                    stats.immediateInFoV = immediateVisible;
                    stats.predictedInFoV = predictedVisible;
                    eventsToLog.Add($"{eventTimestamp};;{navAgentName};;{objID};;{objName};;{immediateVisible};;{predictedVisible};;{immediateDistance:F2};;{predictedDistance:F2};;{estimatedSizeBytes}");
                }
            }
        }

        // Log exit events for objects no longer in the scene.
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

        if (eventsToLog.Count > 0)
        {
            string logText = string.Join("\n", eventsToLog);
            File.AppendAllText(logFilePath, logText + "\n");
        }
    }

    /// <summary>
    /// Estimates the size of a GameObject's mesh (in bytes) assuming 12 bytes per triangle.
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
