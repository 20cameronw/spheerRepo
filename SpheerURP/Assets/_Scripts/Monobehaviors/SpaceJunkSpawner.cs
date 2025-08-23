using UnityEngine;

public class SpaceJunkSpawner : MonoBehaviour
{
    public GameObject spaceJunkPrefab;
    public Camera mainCamera;
    public float spawnInterval = 1.5f;
    public float spawnDistanceXY = 40f; // How far outside the screen
    public float fixedZDistance = 50f;  // Constant Z depth
    public float minSpeed = 40f;
    public float maxSpeed = 80f;

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        InvokeRepeating(nameof(SpawnJunk), 1f, spawnInterval);
    }

    void SpawnJunk()
    {
        // Get screen bounds in world space at the fixed Z depth
        Vector3 screenBottomLeft = mainCamera.ViewportToWorldPoint(new Vector3(0, 0, fixedZDistance));
        Vector3 screenTopRight = mainCamera.ViewportToWorldPoint(new Vector3(1, 1, fixedZDistance));

        // Expand the bounds slightly so spawn is off-screen
        float minX = screenBottomLeft.x - spawnDistanceXY;
        float maxX = screenTopRight.x + spawnDistanceXY;
        float minY = screenBottomLeft.y - spawnDistanceXY;
        float maxY = screenTopRight.y + spawnDistanceXY;

        // Pick a random side for spawn
        int side = Random.Range(0, 4); // 0=left,1=right,2=top,3=bottom
        Vector3 startPos = Vector3.zero;
        Vector3 endPos = Vector3.zero;

        if (side == 0) // Left to Right
        {
            startPos = new Vector3(minX, Random.Range(minY, maxY), fixedZDistance);
            endPos = new Vector3(maxX, Random.Range(minY, maxY), fixedZDistance);
        }
        else if (side == 1) // Right to Left
        {
            startPos = new Vector3(maxX, Random.Range(minY, maxY), fixedZDistance);
            endPos = new Vector3(minX, Random.Range(minY, maxY), fixedZDistance);
        }
        else if (side == 2) // Top to Bottom
        {
            startPos = new Vector3(Random.Range(minX, maxX), maxY, fixedZDistance);
            endPos = new Vector3(Random.Range(minX, maxX), minY, fixedZDistance);
        }
        else // Bottom to Top
        {
            startPos = new Vector3(Random.Range(minX, maxX), minY, fixedZDistance);
            endPos = new Vector3(Random.Range(minX, maxX), maxY, fixedZDistance);
        }

        // Spawn the junk
        GameObject junk = Instantiate(spaceJunkPrefab, startPos, Random.rotation);
        SpaceJunk junkScript = junk.GetComponent<SpaceJunk>();

        // Random speed for variety
        junkScript.speed = Random.Range(minSpeed, maxSpeed);
        junkScript.Initialize(endPos);
    }
}
