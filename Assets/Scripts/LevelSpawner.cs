using UnityEngine;
using Unity.MLAgents;

public class LevelSpawner : Agent
{
    // ============================================================
    // PREFAB REFERENCES - Drag & drop your prefabs here
    // ============================================================

    [Header("Obstacle Prefabs")]
    public GameObject hayRollPrefab;
    public GameObject cratePrefab;
    public GameObject highCratePrefab;
    public GameObject widthHayRollPrefab;

    [Header("Character & Snack Prefabs")]
    public GameObject chickenPrefab;    // The AI chicken agent
    public GameObject hunterPrefab;    // The enemy the chicken fears
    public GameObject colaPrefab;       // Snack type 1
    public GameObject nuggetPrefab;     // Snack type 2

    // ============================================================
    // SPAWN BOUNDS - Defines the area where objects can spawn
    // Default: ±11 provides 1.5 unit safety from walls at ±12.5
    // ============================================================

    [Header("Spawn Bounds")]
    public float spawnMinX = -11f;
    public float spawnMaxX = 11f;
    public float spawnMinZ = -11f;
    public float spawnMaxZ = 11f;
    public float hunterMinDistance = 4f;  // Minimum distance from hunter for all spawns

    // ============================================================
    // SPAWN COUNTS - How many of each object to spawn
    // ObstacleMinDistance prevents obstacles from spawning too close
    // ============================================================

    [Header("Obstacle Counts")]
    public int hayRollCount = 3;
    public int crateCount = 2;
    public int highCrateCount = 1;
    public int widthHayRollCount = 1;
    public float obstacleMinDistance = 2.0f;

    [Header("Character & Snack Counts")]
    public int chickenCount = 1;
    public int colaCount = 5;
    public int nuggetCount = 5;

    // ============================================================
    // PRIVATE PARENT OBJECTS - Created at runtime to organize the scene
    // Each type gets its own parent for clean hierarchy
    // ============================================================

    private GameObject obstaclesParent;
    private GameObject chickensParent;
    private GameObject huntersParent;
    private GameObject colaSnacksParent;
    private GameObject nuggetSnacksParent;
    private Vector3 hunterPosition;

    // ============================================================
    // INITIALIZATION - Creates parent containers at runtime
    // ============================================================

    private void Awake()
    {
        CreateParentObjects();
    }

    private void CreateParentObjects()
    {
        // Create a container for each object type to keep the scene organized
        obstaclesParent = new GameObject("Obstacles");
        obstaclesParent.transform.SetParent(transform);

        chickensParent = new GameObject("Chickens");
        chickensParent.transform.SetParent(transform);

        huntersParent = new GameObject("Hunters");
        huntersParent.transform.SetParent(transform);

        colaSnacksParent = new GameObject("ColaSnacks");
        colaSnacksParent.transform.SetParent(transform);

        nuggetSnacksParent = new GameObject("NuggetSnacks");
        nuggetSnacksParent.transform.SetParent(transform);
    }

    // ============================================================
    // PUBLIC SPAWN METHODS - Can be called from Unity or code
    // ============================================================

    /// <summary>
    /// Main spawn method - spawns everything in the correct order.
    /// Order matters: obstacles first, then hunter, then chickens (needs hunter ref),
    /// then snacks (chickens need snack refs).
    /// </summary>
    [ContextMenu("Spawn All")]
    public void SpawnAll()
    {
        EnsureParentObjectsExist();  // Safety check in case called before Awake
        ClearAll();
        SpawnObstacles();
        SpawnHunters();
        SpawnChickens();
        SpawnSnacks();
    }

    /// <summary>
    /// Removes all spawned objects from the scene.
    /// Used before respawning or when resetting.
    /// </summary>
    [ContextMenu("Clear All")]
    public void ClearAll()
    {
        EnsureParentObjectsExist();  // Safety check in case called before Awake
        ClearChildren(obstaclesParent);
        ClearChildren(chickensParent);
        ClearChildren(huntersParent);
        ClearChildren(colaSnacksParent);
        ClearChildren(nuggetSnacksParent);
    }

    /// <summary>
    /// Alias for ClearAll() - same functionality with different name.
    /// </summary>
    [ContextMenu("Despawn All")]
    public void DespawnAll()
    {
        EnsureParentObjectsExist();  // Safety check in case called before Awake
        ClearAll();
    }

    /// <summary>
    /// Ensures parent objects exist before any operation.
    /// Handles case where methods are called before Awake().
    /// </summary>
    private void EnsureParentObjectsExist()
    {
        if (obstaclesParent == null)
        {
            CreateParentObjects();
        }
    }

    // ============================================================
    // PRIVATE HELPER METHODS
    // ============================================================

    /// <summary>
    /// Destroys all child objects of a parent GameObject.
    /// Iterates backwards to safely remove during iteration.
    /// Uses DestroyImmediate for Edit Mode, Destroy for Play Mode.
    /// </summary>
    private void ClearChildren(GameObject parent)
    {
        for (int i = parent.transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.transform.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    // ============================================================
    // OBSTACLE SPAWNING - Spawns obstacles with distance checks
    // ============================================================

    /// <summary>
    /// Spawns all 4 obstacle types based on their count settings.
    /// Each type uses GetValidObstaclePosition() to avoid overlapping.
    /// </summary>
    private void SpawnObstacles()
    {
        SpawnObstacleType(hayRollPrefab, hayRollCount);
        SpawnObstacleType(cratePrefab, crateCount);
        SpawnObstacleType(highCratePrefab, highCrateCount);
        SpawnObstacleType(widthHayRollPrefab, widthHayRollCount);
    }

    /// <summary>
    /// Spawns a specific number of one obstacle type.
    /// Each instance gets a random rotation for visual variety.
    /// </summary>
    private void SpawnObstacleType(GameObject prefab, int count)
    {
        if (prefab == null) return;

        for (int i = 0; i < count; i++)
        {
            Vector3? pos = GetValidObstaclePosition();
            if (pos.HasValue)
            {
                GameObject obj = Instantiate(prefab, pos.Value, Quaternion.identity, obstaclesParent.transform);
                RandomizeRotation(obj);
            }
        }
    }

    /// <summary>
    /// Finds a valid spawn position that is at least obstacleMinDistance
    /// away from all other existing obstacles AND hunterMinDistance from the hunter.
    /// Tries up to 50 times, then falls back to random position if no valid one found.
    /// </summary>
    private Vector3? GetValidObstaclePosition()
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            Vector3 candidate = GetRandomPosition();

            // Check distance to hunter (only X/Z, ignore Y for 2D distance)
            Vector3 candidateXZ = new Vector3(candidate.x, 0, candidate.z);
            Vector3 hunterXZ = new Vector3(hunterPosition.x, 0, hunterPosition.z);
            if (Vector3.Distance(candidateXZ, hunterXZ) < hunterMinDistance)
            {
                continue;  // Try next position
            }

            bool valid = true;
            foreach (Transform child in obstaclesParent.transform)
            {
                // Check distance to each existing obstacle
                if (Vector3.Distance(candidate, child.position) < obstacleMinDistance)
                {
                    valid = false;
                    break;
                }
            }

            if (valid) return candidate;
        }

        // Fallback: return random position even if it might overlap
        return GetRandomPosition();
    }

    // ============================================================
    // HUNTER SPAWNING - Spawns the enemy in the center
    // ============================================================

    /// <summary>
    /// Spawns the hunter (enemy) at the center of the arena.
    /// Uses the Y position from the prefab's localPosition.
    /// The chicken agent uses this transform to detect and fear the hunter.
    /// </summary>
    private void SpawnHunters()
    {
        if (hunterPrefab == null) return;

        // Get the Y position directly from the prefab's localPosition
        float prefabY = hunterPrefab.transform.localPosition.y;
        hunterPosition = new Vector3(0, prefabY, 0);
        GameObject hunter = Instantiate(hunterPrefab, hunterPosition, Quaternion.identity, huntersParent.transform);
    }

    // ============================================================
    // CHICKEN SPAWNING - Spawns AI agents with proper configuration
    // ============================================================

    /// <summary>
    /// Spawns chicken agents and configures them properly.
    /// Each chicken gets references to all snacks and the hunter.
    /// This is why chickens are spawned after snacks and hunters.
    /// </summary>
    private void SpawnChickens()
    {
        if (chickenPrefab == null) return;

        // Get hunter transform for chicken configuration (must spawn after SpawnHunters)
        Transform hunterTransform = huntersParent.transform.childCount > 0 ? huntersParent.transform.GetChild(0) : null;
        float prefabY = chickenPrefab.transform.localPosition.y;

        for (int i = 0; i < chickenCount; i++)
        {
            Vector3 pos = GetPositionAvoidingHunter();
            pos.y = prefabY;
            GameObject chicken = Instantiate(chickenPrefab, pos, Quaternion.identity, chickensParent.transform);

            chicken_agent agent = chicken.GetComponent<chicken_agent>();
            if (agent != null)
            {
                // Collect all snacks from both parents into one array for the chicken
                GameObject[] allSnacks = new GameObject[colaSnacksParent.transform.childCount + nuggetSnacksParent.transform.childCount];

                int idx = 0;
                foreach (Transform cola in colaSnacksParent.transform)
                {
                    allSnacks[idx++] = cola.gameObject;
                }
                foreach (Transform nugget in nuggetSnacksParent.transform)
                {
                    allSnacks[idx++] = nugget.gameObject;
                }

                // Configure the chicken agent
                agent.snacks = allSnacks;
                agent.shooter = hunterTransform;  // The chicken will fear this
            }
        }
    }

    // ============================================================
    // SNACK SPAWNING - Spawns collectible items (no distance checks)
    // ============================================================

    /// <summary>
    /// Spawns both types of snacks in their respective parent containers.
    /// Snacks respect hunterMinDistance but can cluster together.
    /// </summary>
    private void SpawnSnacks()
    {
        SpawnSnackType(colaPrefab, colaCount, colaSnacksParent);
        SpawnSnackType(nuggetPrefab, nuggetCount, nuggetSnacksParent);
    }

    /// <summary>
    /// Spawns a specific number of one snack type into a specific parent.
    /// </summary>
    private void SpawnSnackType(GameObject prefab, int count, GameObject parent)
    {
        if (prefab == null) return;

        float prefabY = prefab.transform.localPosition.y;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = GetPositionAvoidingHunter();
            pos.y = prefabY;
            Instantiate(prefab, pos, Quaternion.identity, parent.transform);
        }
    }

    // ============================================================
    // UTILITY METHODS - Random position and rotation helpers
    // ============================================================

    /// <summary>
    /// Generates a random XZ position within the spawn bounds.
    /// Y is always 0 (ground level).
    /// </summary>
    private Vector3 GetRandomPosition()
    {
        float x = Random.Range(spawnMinX, spawnMaxX);
        float z = Random.Range(spawnMinZ, spawnMaxZ);
        return new Vector3(x, 0, z);
    }

    /// <summary>
    /// Generates a random position that is at least hunterMinDistance from the hunter.
    /// Tries up to 50 times, then falls back to random position.
    /// </summary>
    private Vector3 GetPositionAvoidingHunter()
    {
        Vector3 hunterXZ = new Vector3(hunterPosition.x, 0, hunterPosition.z);

        for (int attempt = 0; attempt < 50; attempt++)
        {
            Vector3 candidate = GetRandomPosition();
            Vector3 candidateXZ = new Vector3(candidate.x, 0, candidate.z);

            if (Vector3.Distance(candidateXZ, hunterXZ) >= hunterMinDistance)
            {
                return candidate;
            }
        }

        // Fallback: return random position even if too close to hunter
        return GetRandomPosition();
    }

    /// <summary>
    /// Applies a random Y-axis rotation to an object for visual variety.
    /// </summary>
    private void RandomizeRotation(GameObject obj)
    {
        float randomY = Random.Range(0f, 360f);
        obj.transform.rotation = Quaternion.Euler(0f, randomY, 0f);
    }

    // ============================================================
    // ML-AGENTS INTEGRATION - Episode reset handling
    // ============================================================

    /// <summary>
    /// Called automatically by Unity ML-Agents when an episode ends.
    /// Resets the level by clearing and respawning everything.
    /// </summary>
    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();
        ClearAll();
        SpawnAll();
    }
}