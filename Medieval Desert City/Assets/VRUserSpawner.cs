using System;
using System.Globalization;
using Cinemachine.Utility;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public class RandomPrefabSpawner : MonoBehaviour
{
    // Prefabs for instantiation.
    public GameObject Leader_VR_user;
    public GameObject Follower_VR_User;

    // Number of leaders to be spawned.
    public int numberOfLeaders = 1;
    // Number of followers to spawn per leader.
    public int followersPerLeader = 5;

    // Minimum and maximum positions for random placement of leaders.
    public Vector3 minPosition = new Vector3(-10f, 0f, -10f);
    public Vector3 maxPosition = new Vector3(10f, 0f, 10f);


    // select the spawn area from a collection of spawn points
    private Vector3[] spawnPoints = 
    {
        new Vector3(-122.54f, 0f, 190.93f),
        new Vector3(0f, 0f, 200f),
        new Vector3(-154.6f, 0f, 129.5f),
        new Vector3(-179.7f, 0f, 75.1f),
        new Vector3(-50f, 0f, 42f),
        new Vector3(-60f, 0f, 85f),
        new Vector3(0f, 0f, 37f),
        new Vector3(-10f, 0f, 120f),
        new Vector3(59.5f, 0f, 177.6f),
        new Vector3(85.7f, 0f, 4.69f),
        new Vector3(78.3f, 0f, -88.3f),
        new Vector3(-29.5f, 0f, -109.3f),
        new Vector3(-73.1f, 0f, -147.8f),
        new Vector3(-75.4f, 0f, -47.10f),

    };
    // Radius within which followers will be spawned around their leader.
    public float followerSpawnRadius = 3f;

    void Start()
    {
        // Check if prefabs are assigned in the Inspector.
        if (Leader_VR_user == null || Follower_VR_User == null)
        {
            Debug.LogError("VR_user prefab is not assigned in the Inspector.");
            return;
        }

        // Loop to instantiate the specified number of leaders.
        for (int i = 0; i < numberOfLeaders; i++)
        {
            // pick a random spawn point from the array
            int randomIndex = UnityEngine.Random.Range(0, spawnPoints.Length);
            Vector3 spawnPoint = spawnPoints[randomIndex];
            // set the leader position to the selected spawn point
            Vector3 leaderPosition = spawnPoint;
            // Instantiate the leader prefab at the random position with no rotation.
            GameObject newLeader = Instantiate(Leader_VR_user, leaderPosition, Quaternion.identity);
            newLeader.name = "Leader_VR_user_" + i;

            // For each leader, spawn the specified number of followers in proximity.
            for (int j = 0; j < followersPerLeader; j++)
            {
                // Generate a random offset so that followers spawn near their leader.
                // Here, the offset is only applied on the XZ plane.
                Vector3 offset = new Vector3(UnityEngine.Random.Range(-followerSpawnRadius, followerSpawnRadius), 0f, UnityEngine.Random.Range(-followerSpawnRadius, followerSpawnRadius));
                Vector3 followerPosition = leaderPosition + offset;

                // Instantiate the follower prefab at the calculated follower position.
                GameObject newFollower = Instantiate(Follower_VR_User, followerPosition, Quaternion.identity);
                newFollower.name = "Follower_VR_User_" + i + "_" + j; // Uses leader and follower indices.
            }
        }
    }
}
