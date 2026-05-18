using UnityEngine;
using Unity.MLAgents;
using Unity.XR.CoreUtils;
using System.Collections;

public class LevelSpawner : MonoBehaviour
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
    public Transform xrOrigin;          // The VR player replacing the shooter
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
    public float xrOriginMinDistance = 4f;  // Minimum distance from player for all spawns

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

    [Header("Collision Layers")]
    public LayerMask obstaclesLayer;  // Layer for obstacles to avoid spawning inside objects

    // ============================================================
    // PRIVATE PARENT OBJECTS - Created at runtime to organize the scene
    // Each type gets its own parent for clean hierarchy
    // ============================================================

    private GameObject obstaclesParent;
    private GameObject chickensParent;
    private GameObject colaSnacksParent;
    private GameObject nuggetSnacksParent;
    private Vector3 xrOriginPosition;
    
    private Vector3 initialPlayerPosition;
    private Quaternion initialPlayerRotation;

    // ============================================================
    // INITIALIZATION - Creates parent containers at runtime
    // ============================================================

    private void Awake()
    {
        CreateParentObjects();
    }

    private void Start()
    {
        if (xrOrigin != null)
        {
            initialPlayerPosition = xrOrigin.position;
            initialPlayerRotation = xrOrigin.rotation;
        }
        ResetLevel();
    }

    private void CreateParentObjects()
    {
        // Create a container for each object type to keep the scene organized
        obstaclesParent = new GameObject("Obstacles");
        obstaclesParent.transform.SetParent(transform);

        chickensParent = new GameObject("Chickens");
        chickensParent.transform.SetParent(transform);

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
        ResetPlayerPosition();
        SpawnSnacks();
        RespawnOrUpdateChickens();
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
        // We do NOT clear chickens here, because destroying an active Agent crashes ML-Agents.
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

            // Check distance to player (only X/Z, ignore Y for 2D distance)
            Vector3 candidateXZ = new Vector3(candidate.x, 0, candidate.z);
            Vector3 playerXZ = new Vector3(xrOriginPosition.x, 0, xrOriginPosition.z);
            if (Vector3.Distance(candidateXZ, playerXZ) < xrOriginMinDistance)
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
    // PLAYER POSITIONING - Teleports VR player to center
    // ============================================================

    /// <summary>
    /// Teleports the XR Origin to the center of the arena instead of spawning it.
    /// The chicken agent uses this transform to detect and fear the player.
    /// </summary>
    private void ResetPlayerPosition()
    {
        if (xrOrigin == null) return;

        Transform playerRoot = xrOrigin;
        
        // --- 1. PROBEER DE OFFICIËLE XR ORIGIN TELEPORTATIE ---
        // Dit is essentieel als de speler fysiek heeft rondgelopen in z'n kamer (lokale camera offset)
        XROrigin originComponent = playerRoot.GetComponent<XROrigin>();
        if (originComponent == null)
        {
            originComponent = playerRoot.GetComponentInParent<XROrigin>();
        }

        if (originComponent != null)
        {
            // Omdat MoveCameraToWorldLocation de CAMERA (het hoofd) verplaatst, moeten we de hoogte van de speler optellen bij de spawn positie.
            // Anders wordt het hoofd op de grond (Y=0) geplaatst.
            Vector3 targetHeadPosition = initialPlayerPosition;
            targetHeadPosition.y += originComponent.CameraInOriginSpaceHeight;

            // Verplaats de CAMERA exact naar de berekende hoofd-positie
            originComponent.MoveCameraToWorldLocation(targetHeadPosition);
            
            // Match ook de rotatie zodat hij weer recht naar voren kijkt
            originComponent.MatchOriginUpCameraForward(Vector3.up, initialPlayerRotation * Vector3.forward);
            
            xrOriginPosition = originComponent.transform.position;
            return;
        }

        // --- 2. FALLBACK ALS HET GEEN XR ORIGIN IS ---
        CharacterController cc = playerRoot.GetComponent<CharacterController>();
        
        if (cc == null)
        {
            cc = playerRoot.GetComponentInParent<CharacterController>();
            if (cc != null) playerRoot = cc.transform;
        }

        if (cc != null) cc.enabled = false;

        Rigidbody rb = playerRoot.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.position = initialPlayerPosition;
        }

        playerRoot.position = initialPlayerPosition;
        playerRoot.rotation = initialPlayerRotation;

        Physics.SyncTransforms();

        if (cc != null) cc.enabled = true;

        xrOriginPosition = playerRoot.position;
    }

    // ============================================================
    // CHICKEN SPAWNING - Spawns AI agents with proper configuration
    // ============================================================

    /// <summary>
    /// Spawns chicken agents if needed, and repositions/updates existing ones.
    /// Reusing the agents prevents ML-Agents from crashing during a step.
    /// </summary>
    private void RespawnOrUpdateChickens()
    {
        if (chickenPrefab == null) return;

        Transform playerTransform = xrOrigin;
        float prefabY = chickenPrefab.transform.localPosition.y;

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

        // Spawn missing chickens if we don't have enough
        int currentChickens = chickensParent.transform.childCount;
        for (int i = currentChickens; i < chickenCount; i++)
        {
            Instantiate(chickenPrefab, Vector3.zero, Quaternion.identity, chickensParent.transform);
        }

        // Update all chickens (positions and references)
        foreach (Transform child in chickensParent.transform)
        {
            Vector3 pos = GetPositionAvoidingPlayer();
            pos.y = prefabY;

            chicken_agent agent = child.GetComponent<chicken_agent>();
            if (agent != null)
            {
                agent.snacks = allSnacks;
                agent.xrOrigin = playerTransform;
                agent.Revive(pos);
            }
            else
            {
                child.position = pos;
                Rigidbody rb = child.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = Vector3.zero;
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
            Vector3 pos = GetPositionAvoidingPlayer();
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
    /// Generates a random position that is at least xrOriginMinDistance from the player.
    /// Tries up to 50 times, then falls back to random position.
    /// </summary>
    private Vector3 GetPositionAvoidingPlayer()
    {
        Vector3 playerXZ = new Vector3(xrOriginPosition.x, 0, xrOriginPosition.z);
        float checkRadius = 1.0f;  // Radius to check for collision

        for (int attempt = 0; attempt < 50; attempt++)
        {
            Vector3 candidate = GetRandomPosition();
            Vector3 candidateXZ = new Vector3(candidate.x, 0, candidate.z);

            if (Vector3.Distance(candidateXZ, playerXZ) >= xrOriginMinDistance)
            {
                // Check if position is free of obstacles
                if (obstaclesLayer.value != 0 && 
                    Physics.OverlapSphere(candidate, checkRadius, obstaclesLayer).Length == 0)
                {
                    return candidate;
                }
                else if (obstaclesLayer.value == 0)
                {
                    // If no layer set, just return position
                    return candidate;
                }
            }
        }

        // Fallback: return random position even if too close to player
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
    /// Resets the level by clearing and respawning everything.
    /// </summary>
    public void ResetLevel()
    {
        StartCoroutine(ResetLevelRoutine());
    }

    private IEnumerator ResetLevelRoutine()
    {
        // Wacht tot het einde van de frame om conflicten met VR Locomotion (Update/LateUpdate) te vermijden
        yield return new WaitForEndOfFrame();

        ClearAll();
        SpawnObstacles();
        ResetPlayerPosition();
        SpawnSnacks();
        RespawnOrUpdateChickens();
    }

    /// <summary>
    /// Checks if all chickens are inactive (dead). If so, triggers a level reset.
    /// </summary>
    public void CheckAllChickensDeadAndReset()
    {
        bool anyChickenActive = false;
        foreach (Transform child in chickensParent.transform)
        {
            chicken_agent agent = child.GetComponent<chicken_agent>();
            if (agent != null && !agent.isDead)
            {
                anyChickenActive = true;
                break;
            }
        }

        if (!anyChickenActive)
        {
            ResetLevel();
        }
    }
}