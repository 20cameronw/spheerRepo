using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldSpawner : MonoBehaviour
{
    [HideInInspector]
    public GameObject CurrentWorldGO;

    public GameObject CurrentWorld;

    [Header("Setup Fields")]
    [SerializeField] List<GameObject> WorldsList;
    [SerializeField] private List<GameObject> structuresGOList;
    [SerializeField] private SphereCollider surface;

    [Space(10)]
    [Header("World Data")]
    [SerializeField] private WorldsListSO worldsListSO;

    [Space(10)]
    [Header("Orbit Settings")]
    [SerializeField] private SphereCollider orbitSC;
    [SerializeField] private float xOrbitSpeed;
    [SerializeField] private float yOrbitSpeed;
    [SerializeField] private float zOrbitSpeed;

    private GameObject orbitGO;

    private int objectsSpawned = 0;
    private List<GameObject> spawnedObjects = new List<GameObject>();

    // ── Slot system ──────────────────────────────────────────────────────────
    // Slot positions are stored in CurrentWorld local space so they rotate
    // with the world.  Index N in slotPositions maps 1-to-1 with slotOccupied[N].
    private List<Vector3> slotPositions = new List<Vector3>();
    private bool[] slotOccupied = new bool[0];
    private int currentMaxSlots = 20;
    private const int DEFAULT_MAX_SLOTS = 20;
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable() => EventManager.OnClicked += ExpandAndShrink;
    private void OnDisable() => EventManager.OnClicked -= ExpandAndShrink;

    private void Start()
    {
        spawnOrbit();
    }

    void spawnOrbit()
    {
        orbitGO = new GameObject("OrbitGO");
        orbitGO.transform.SetParent(transform);
        orbitGO.transform.position = transform.position;
        orbitGO.AddComponent<Rotate>();
        orbitGO.GetComponent<Rotate>().SetSpeeds(xOrbitSpeed, yOrbitSpeed, zOrbitSpeed);
    }

    public void SetCurrentWorld(int WorldIndex)
    {
        if (CurrentWorldGO == null)
        {
            CurrentWorldGO = (GameObject)WorldsList[WorldIndex];
            SpawnWorld();
        }
        else
        {
            DeleteCurrentWorld();
            CurrentWorldGO = (GameObject)WorldsList[WorldIndex];
            SpawnWorld();
        }
        GenerateSlots(WorldIndex);
    }

    private void DeleteCurrentWorld()
    {
        Destroy(CurrentWorld);

        foreach (GameObject obj in spawnedObjects)
        {
            Destroy(obj);
        }
        spawnedObjects.Clear();
    }

    public void SpawnWorld()
    {
        CurrentWorld = Instantiate(CurrentWorldGO, transform);
    }

    // Threshold for classifying two Fibonacci-sphere points as "neighbors":
    // empirically ~0.75–0.85 of surface.radius works across slot counts of 12–40.
    private const float NEIGHBOR_DISTANCE_THRESHOLD = 0.8f;

    /// <summary>
    /// Generates <see cref="currentMaxSlots"/> evenly-distributed points on
    /// the world surface using the Fibonacci sphere algorithm.
    /// Positions are in CurrentWorld local space.
    /// </summary>
    private void GenerateSlots(int worldIndex)
    {
        slotPositions.Clear();
        currentMaxSlots = GetConfiguredMaxSlots(worldIndex);

        slotOccupied = new bool[currentMaxSlots];

        float radius = surface.radius;
        float goldenRatio = (1f + Mathf.Sqrt(5f)) * 0.5f;

        for (int i = 0; i < currentMaxSlots; i++)
        {
            float theta = 2f * Mathf.PI * i / goldenRatio;
            float phi = Mathf.Acos(1f - 2f * (i + 0.5f) / currentMaxSlots);

            float x = Mathf.Sin(phi) * Mathf.Cos(theta);
            float y = Mathf.Sin(phi) * Mathf.Sin(theta);
            float z = Mathf.Cos(phi);

            slotPositions.Add(new Vector3(x, y, z) * radius);
        }
    }

    private int GetConfiguredMaxSlots(int worldIndex)
    {
        int configured = DEFAULT_MAX_SLOTS;
        if (worldsListSO != null && worldIndex >= 0 && worldIndex < worldsListSO.worldsList.Length)
        {
            configured = worldsListSO.worldsList[worldIndex].maxBuildingSlots;
        }

        if (configured < 1)
        {
            Debug.LogWarning("[WorldSpawner] maxBuildingSlots was < 1; using fallback default.");
            configured = DEFAULT_MAX_SLOTS;
        }

        return configured;
    }

    // ── Slot queries ─────────────────────────────────────────────────────────

    public int GetMaxSlots() => currentMaxSlots;

    public int GetSlotsUsed()
    {
        int count = 0;
        foreach (bool o in slotOccupied)
            if (o) count++;
        return count;
    }

    public int GetSlotsAvailable()
    {
        EnsureSlotsGeneratedForCurrentWorld();
        return currentMaxSlots - GetSlotsUsed();
    }

    /// <summary>
    /// Returns the world-space positions of all unoccupied slots that can
    /// accommodate a building of the given <paramref name="slotSize"/>,
    /// together with each slot's index so callers avoid a second O(n) lookup.
    /// </summary>
    public List<(Vector3 position, int index)> GetAvailableSlotPositions(int slotSize = 1)
    {
        EnsureSlotsGeneratedForCurrentWorld();
        if (CurrentWorld == null) return new List<(Vector3, int)>();

        var available = new List<(Vector3, int)>();
        for (int i = 0; i < slotPositions.Count; i++)
        {
            if (!slotOccupied[i] && HasEnoughNearbyFreeSlots(i, slotSize))
                available.Add((CurrentWorld.transform.TransformPoint(slotPositions[i]), i));
        }
        return available;
    }

    /// <summary>
    /// Returns true if there are at least <paramref name="slotSize"/> unoccupied
    /// slots (including the given one) within the neighbourhood of slot
    /// <paramref name="anchorIndex"/>.
    /// </summary>
    private bool HasEnoughNearbyFreeSlots(int anchorIndex, int slotSize)
    {
        if (slotSize <= 1) return true;
        int freeNeighbors = 1; // the anchor itself
        Vector3 anchor = slotPositions[anchorIndex];
        for (int j = 0; j < slotPositions.Count && freeNeighbors < slotSize; j++)
        {
            if (j == anchorIndex || slotOccupied[j]) continue;
            float dist = Vector3.Distance(anchor, slotPositions[j]);
            if (dist < surface.radius * NEIGHBOR_DISTANCE_THRESHOLD)
                freeNeighbors++;
        }
        return freeNeighbors >= slotSize;
    }

    /// <summary>
    /// Occupies the slot nearest to <paramref name="worldPos"/> plus
    /// (<paramref name="slotSize"/> - 1) nearest free neighbors.
    /// Returns the index of the primary slot, or -1 if no slot was found.
    /// </summary>
    public int OccupySlot(Vector3 worldPos, int slotSize = 1)
    {
        EnsureSlotsGeneratedForCurrentWorld();
        if (CurrentWorld == null) return -1;

        int nearest = FindNearestSlot(worldPos, freeOnly: true);
        if (nearest < 0) return -1;
        slotOccupied[nearest] = true;

        // Occupy additional neighbor slots for larger buildings
        int extraNeeded = slotSize - 1;
        if (extraNeeded > 0)
        {
            Vector3 anchor = slotPositions[nearest];
            List<int> neighborOrder = GetSlotsSortedByDistance(anchor);
            foreach (int idx in neighborOrder)
            {
                if (extraNeeded <= 0) break;
                if (!slotOccupied[idx])
                {
                    slotOccupied[idx] = true;
                    extraNeeded--;
                }
            }
        }

        return nearest;
    }

    /// <summary>
    /// Marks the next <paramref name="slotSize"/> available slots as occupied.
    /// Used when re-spawning buildings from save data.
    /// </summary>
    public void OccupyNextAvailableSlots(int slotSize)
    {
        int occupied = 0;
        for (int i = 0; i < slotOccupied.Length && occupied < slotSize; i++)
        {
            if (!slotOccupied[i])
            {
                slotOccupied[i] = true;
                occupied++;
            }
        }
    }

    /// <summary>
    /// Returns the index of the slot (free or occupied) whose local position
    /// is nearest to <paramref name="worldPos"/>.  Pass <paramref name="freeOnly"/>
    /// = <c>true</c> to restrict to unoccupied slots.  Returns -1 if none found.
    /// </summary>
    private int FindNearestSlot(Vector3 worldPos, bool freeOnly)
    {
        if (CurrentWorld == null) return -1;
        Vector3 localPos = CurrentWorld.transform.InverseTransformPoint(worldPos);
        int bestIdx = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < slotPositions.Count; i++)
        {
            if (freeOnly && slotOccupied[i]) continue;
            float d = Vector3.Distance(localPos, slotPositions[i]);
            if (d < bestDist) { bestDist = d; bestIdx = i; }
        }
        return bestIdx;
    }

    private List<int> GetSlotsSortedByDistance(Vector3 localAnchor)
    {
        List<int> indices = new List<int>();
        for (int i = 0; i < slotPositions.Count; i++) indices.Add(i);
        indices.Sort((a, b) =>
            Vector3.Distance(slotPositions[a], localAnchor)
            .CompareTo(Vector3.Distance(slotPositions[b], localAnchor)));
        return indices;
    }

    // ── Spawn helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a building at a specific world-space position (used by
    /// <see cref="PlacementManager"/> after the player confirms a slot).
    /// Does NOT modify slot occupancy — call <see cref="OccupySlot"/> first.
    /// </summary>
    public void SpawnAtPosition(int index, Vector3 worldPos)
    {
        GameObject newObject = Instantiate(structuresGOList[index], worldPos, Quaternion.identity);
        newObject.transform.SetParent(CurrentWorld.transform);
        newObject.transform.LookAt(CurrentWorld.transform.position);
        newObject.transform.Rotate(-90, 0, 0);
        spawnedObjects.Add(newObject);
        newObject.name = TransactionManager.Instance.structuresPanelInfo.shopItemsSO[index].name
                         + " " + objectsSpawned;
        objectsSpawned++;
    }

    public void ExpandAndShrink()
    {
        StartCoroutine(ExpandAndShrinkCoroutine(.04f));
    }

    private IEnumerator ExpandAndShrinkCoroutine(float duration)
    {
        Vector3 originalScale = new Vector3(1f, 1f, 1f);
        Vector3 expandedScale = originalScale * 1.1f;

        float halfDuration = duration / 2f;
        float elapsedTime = 0f;

        while (elapsedTime < halfDuration)
        {
            transform.localScale = Vector3.Lerp(originalScale, expandedScale, elapsedTime / halfDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        elapsedTime = 0f;

        while (elapsedTime < halfDuration)
        {
            transform.localScale = Vector3.Lerp(expandedScale, originalScale, elapsedTime / halfDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.localScale = originalScale;
    }

    public void spawnObject(int index, float passive)
    {
        if (TransactionManager.Instance.structuresPanelInfo.shopItemsSO[index].isInOrbit)
            spawnInOrbit(index, passive);
        else
            spawnOnSurface(index, passive);

        objectsSpawned++;
    }

    public void spawnOnSurface(int index, float passive)
    {
        // During load: place near a random surface point and occupy the nearest slot.
        Vector3 randomWorldPos = UnityEngine.Random.onUnitSphere * surface.radius
                                 + CurrentWorld.transform.position;
        int slotSize = Mathf.Max(1, TransactionManager.Instance.structuresPanelInfo.shopItemsSO[index].slotSize);
        OccupySlot(randomWorldPos, slotSize);

        GameObject newObject = Instantiate(structuresGOList[index], randomWorldPos, Quaternion.identity) as GameObject;
        newObject.transform.SetParent(CurrentWorld.transform);
        newObject.transform.LookAt(CurrentWorld.transform.position);
        newObject.transform.Rotate(-90, 0, 0);
        spawnedObjects.Add(newObject);
        newObject.gameObject.name = TransactionManager.Instance.structuresPanelInfo.shopItemsSO[index].name + " " + objectsSpawned;
    }

    public void LoadObjects(int count, int index)
    {
        StartCoroutine(delayLoadObjects(count, index));
    }

    private IEnumerator delayLoadObjects(int count, int index)
    {
        yield return new WaitForSeconds(.5f);
        SpawnManyObjects(count, index);
    }

    public void SpawnManyObjects(int count, int index)
    {
        for (int i = 0; i < count; i++)
        {
            spawnObject(index, TransactionManager.Instance.structuresPanelInfo.shopItemsSO[index].bonus);
        }
    }

    public void spawnInOrbit(int index, float passive)
    {
        Vector3 spawnPosition = UnityEngine.Random.onUnitSphere * orbitSC.radius + orbitGO.transform.position;
        Quaternion spawnRotation = Quaternion.identity;
        GameObject newObject = Instantiate(structuresGOList[index], spawnPosition, spawnRotation) as GameObject;
        newObject.transform.SetParent(orbitGO.transform);
        newObject.transform.LookAt(orbitGO.transform.position);
        newObject.transform.Rotate(-90, 0, 0);
        spawnedObjects.Add(newObject);
        newObject.gameObject.name = TransactionManager.Instance.structuresPanelInfo.shopItemsSO[index].name + " " + objectsSpawned;
    }

    public void removeObject(int index)
    {
        for (int i = 0; i < spawnedObjects.Count; i++)
        {
            string name = spawnedObjects[i].gameObject.name.Split(" ")[0];
            Debug.Log("name.Split(\" \")[0]: " + name);
            if (name == TransactionManager.Instance.structuresPanelInfo.shopItemsSO[index].name.Split(" ")[0])
            {
                GameObject newObject = spawnedObjects[i];
                if (newObject != null) {
                    Destroy(newObject); 
                    spawnedObjects.RemoveAt(i);
                }
                
                return;
            }
        }
    }

    // ── Auto-rotation control (used by PlacementManager) ─────────────────────

    /// <summary>
    /// Enables or disables the auto-rotation on the current world and orbit ring.
    /// Call with <c>false</c> when entering placement mode so the player's
    /// drag controls the rotation instead.
    /// </summary>
    public void SetAutoRotate(bool enabled)
    {
        if (CurrentWorld != null)
        {
            Rotate worldRotate = CurrentWorld.GetComponent<Rotate>();
            if (worldRotate != null) worldRotate.enabled = enabled;
        }
        if (orbitGO != null)
        {
            Rotate orbitRotate = orbitGO.GetComponent<Rotate>();
            if (orbitRotate != null) orbitRotate.enabled = enabled;
        }
    }

    // ── Accessors used by PlacementManager ───────────────────────────────────

    public Vector3 GetWorldCenter() =>
        CurrentWorld != null ? CurrentWorld.transform.position : transform.position;

    public float GetSurfaceRadius() => surface.radius;

    private void EnsureSlotsGeneratedForCurrentWorld()
    {
        if (CurrentWorld == null || slotPositions.Count > 0) return;

        int worldIndex = WorldsList.IndexOf(CurrentWorldGO);
        GenerateSlots(worldIndex >= 0 ? worldIndex : 0);
    }
}
