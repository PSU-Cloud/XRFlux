using UnityEngine;

public class RandomCameraMovement : MonoBehaviour
{
    public float speed = 5.0f;  // Speed of the camera movement
    public float changeDirectionInterval = 3.0f;  // Time interval to change direction
    private Vector3 direction;
    private float timer;

    void Start()
    {
        ChangeDirection();
    }

    void Update()
    {
        // Move the camera
        transform.Translate(direction * speed * Time.deltaTime);

        // Check for collisions and change direction if needed
        if (Physics.Raycast(transform.position, transform.forward, 1.0f))
        {
            ChangeDirection();
        }

        // // Update timer and change direction at intervals
        // timer += Time.deltaTime;
        // if (timer > changeDirectionInterval)
        // {
        //     ChangeDirection();
        //     timer = 0;
        // }
    }

    void ChangeDirection()
    {
        // Change to a random direction
        float randomAngle = Random.Range(0, 360);
        direction = new Vector3(Mathf.Cos(randomAngle), 0, Mathf.Sin(randomAngle));
    }
}
