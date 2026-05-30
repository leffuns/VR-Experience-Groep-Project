using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using System.Text;

/*
    1.  Space Size Aanpassen: We hebben zojuist observaties voor de 3 dichtstbijzijnde snacks toegevoegd.
        Je moet nu de Space Size in je Behavior Parameters component op de kip veranderen van 8 naar 17!
    2.  Snacks Instellen: Selecteer al je cola-blikjes en kippennuggets in de scene. Verander hun Tag naar Snack.
    3.  Triggers Maken: Zorg dat de Colliders op je snacks staan aangevinkt als Is Trigger.
    4.  De Array Vullen: Sleep al je snacks vanuit je scene naar de Snacks array op het script van de kip.
*/

[RequireComponent(typeof(Rigidbody))]
public class chicken_agent : Agent
{
    [Header("Doelwit & Beweging")]
    public Transform xrOrigin;
    public float moveSpeed = 5f;

    [Header("Radar Instellingen")]
    public float detectieRadius = 10f;
    public LayerMask xrOriginLayer;
    public LayerMask obstaclesLayer;

    [Header("Audio")]
    [Tooltip("Drag your different chicken death sounds here")]
    public AudioClip[] deathSounds;
    [Range(1f, 50f)]
    [Tooltip("Tot hoeveel meter het geluid op z'n allerhardst te horen is.")]
    public float audioMinRadius = 5f;
    [Range(10f, 200f)]
    [Tooltip("De maximale afstand in meters waarop je het geluid nog net kunt horen.")]
    public float audioMaxRadius = 60f;

    [Header("Visual Effects")]
    [Tooltip("Sleep je veren-explosie prefab hierin")]
    public GameObject featherExplosionPrefab;

    [Header("Honger & Snacks (Kannibalisme?!)")]
    public float maxHonger = 500f;
    [Tooltip("Hoeveel honger er per stap (OnActionReceived) afgaat.")]
    public float hongerAfnamePerStap = 0.1f;
    public float hongerHerstelPerSnack = 40f;
    [Tooltip("Sleep je cola en nuggets hierin zodat het script ze kan resetten.")]
    public GameObject[] snacks;

    [HideInInspector]
    public bool isDead = false;

    [Header("LOS Cache")]
    [Tooltip("Minimale afstand (in meters) die kip of hunter moet verplaatsten voordat LOS opnieuw berekend wordt")]
    public float losCacheAfstand = 1.0f;

    private float huidigeHonger;
    private Rigidbody rb;
    private Vector3 targetDirection;
    private Collider col;
    private Renderer[] renderers;
    private bool zietXROriginCache;
    private bool vorigeLOS;
    private Vector3 laatsteLOSKipPos;
    private Vector3 laatsteLOSHunterPos;
    private bool laatsteLOSResultaat;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        col = GetComponent<Collider>();
        renderers = GetComponentsInChildren<Renderer>();

        LogLayerInfo();
    }

    private void LogLayerInfo()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"[LOS] obstaclesLayer ({obstaclesLayer.value}): ");
        for (int i = 0; i < 32; i++)
        {
            if ((obstaclesLayer.value & (1 << i)) != 0)
                sb.Append($"[{i}:{LayerMask.LayerToName(i)}] ");
        }
        Debug.Log(sb.ToString());

        sb.Clear();
        sb.Append($"[LOS] xrOriginLayer ({xrOriginLayer.value}): ");
        for (int i = 0; i < 32; i++)
        {
            if ((xrOriginLayer.value & (1 << i)) != 0)
                sb.Append($"[{i}:{LayerMask.LayerToName(i)}] ");
        }
        Debug.Log(sb.ToString());
    }

    public override void OnEpisodeBegin()
    {
        rb.linearVelocity = Vector3.zero;

        // Vul de maag weer bij de start
        huidigeHonger = maxHonger;

        // VIND ALLE SNACKS AAN HET BEGIN VAN ELKE EPISODE
        snacks = GameObject.FindGameObjectsWithTag("Snack");

        // Controleer of er wel snacks zijn, om fouten te voorkomen
        if (snacks.Length == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] Waarschuwing: Geen objecten met tag 'Snack' gevonden bij de start van deze episode!");
        }

        // Reset alle gevonden snacks zodat ze weer actief/zichtbaar zijn
        foreach (GameObject snack in snacks)
        {
            if (snack != null) snack.SetActive(true);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (isDead)
        {
            sensor.AddObservation(Vector3.zero); // 3  — positie
            sensor.AddObservation(Vector3.zero); // 3  — speler positie
            sensor.AddObservation(0f);           // 1  — ziet speler?
            sensor.AddObservation(0f);           // 1  — honger
            sensor.AddObservation(Vector3.zero); // 3  — snack 1
            sensor.AddObservation(Vector3.zero); // 3  — snack 2
            sensor.AddObservation(Vector3.zero); // 3  — snack 3
            return;
        }

        zietXROriginCache = KanXROriginZien();

        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(xrOrigin.localPosition);
        sensor.AddObservation(zietXROriginCache ? 1f : 0f);

        // De kip moet weten hoe hongerig hij is. (Genormaliseerd)
        sensor.AddObservation(huidigeHonger / maxHonger);

        // --- NIEUW: OBSERVEER DE 3 DICHTSBIJZINDSTE SNACKS ---
        List<GameObject> actieveSnacks = new List<GameObject>();

        // Filter alle snacks die momenteel actief (nog niet opgegeten) zijn
        foreach (GameObject snack in snacks)
        {
            if (snack != null && snack.activeSelf)
            {
                actieveSnacks.Add(snack);
            }
        }

        // Sorteer de snacks op afstand (dichtstbijzijnde eerst)
        actieveSnacks.Sort((a, b) =>
            Vector3.Distance(transform.position, a.transform.position).CompareTo(
            Vector3.Distance(transform.position, b.transform.position))
        );

        // Geef de relatieve positie van de top 3 snacks door aan het brein
        for (int i = 0; i < 3; i++)
        {
            if (i < actieveSnacks.Count)
            {
                // Relatieve positie (waar is de snack ten opzichte van de kip)
                Vector3 relatievePos = actieveSnacks[i].transform.position - transform.position;
                sensor.AddObservation(relatievePos);
            }
            else
            {
                // Als er minder dan 3 snacks over zijn, vul de rest op met nullen (padding)
                sensor.AddObservation(Vector3.zero);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (isDead) return;

        // [1] LIVING COST — maakt stilzitten netto kosten
        AddReward(-0.002f);

        // [2] VALCONTROLE — van de rand vallen is fataal
        if (transform.position.y < -1.0f)
        {
            PlayRandomDeathSound();
            PlayDeathEffect();
            AddReward(-20.0f);
            TriggerLevelReset();
            return;
        }

        // [3] RANDWAARSCHUWING — subtiele straf voor rand naderen
        if (transform.position.y < -0.3f)
        {
            float t = Mathf.InverseLerp(-1.0f, -0.3f, transform.position.y);
            AddReward(-0.02f * t);
        }

        // [4] HONGER UPDATE
        huidigeHonger -= hongerAfnamePerStap;

        if (huidigeHonger <= 0)
        {
            AddReward(-5.0f);
            TriggerLevelReset();
            return;
        }

        // [5] HIDING vs EXPOSURE (met honger-afhankelijke angst)
        float angstFactor = huidigeHonger / maxHonger;

        if (zietXROriginCache)
        {
            // Volle kip (angst≈1.0): -0.05/stap → direct wegduiken!
            // Hongerige kip (angst≈0.1): -0.005/stap → nog steeds voelbaar
            AddReward(-0.05f * angstFactor);
        }
        else
        {
            // Veilige baseline: netto +0.004 na living cost
            AddReward(0.006f);
        }

        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];

        float threshold = 0.1f;
        if (Mathf.Abs(moveX) < threshold) moveX = 0f;
        if (Mathf.Abs(moveZ) < threshold) moveZ = 0f;

        if (moveX != 0f || moveZ != 0f)
            targetDirection = new Vector3(moveX, 0, moveZ).normalized;
        else
            targetDirection = Vector3.zero;
    }

    // public override void Heuristic(in ActionBuffers actionsOut)
    // {
    //     ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
    //     float h = Input.GetAxisRaw("Horizontal");
    //     float v = Input.GetAxisRaw("Vertical");

    //     Debug.Log($"[Heuristic] Input - Horizontal: {h}, Vertical: {v}");

    //     continuousActions[0] = h;
    //     continuousActions[1] = v;
    // }

    private void FixedUpdate()
    {
        if (targetDirection != Vector3.zero)
        {
            Vector3 velocity = targetDirection * moveSpeed;
            rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
        }
        else
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }
    }

    private bool KanXROriginZien()
    {
        Vector3 huidigeHunterPos = xrOrigin != null ? xrOrigin.position : Vector3.zero;

        if (Vector3.Distance(transform.position, laatsteLOSKipPos) < losCacheAfstand &&
            Vector3.Distance(huidigeHunterPos, laatsteLOSHunterPos) < losCacheAfstand)
        {
            return laatsteLOSResultaat;
        }

        Collider[] xrOrigins = Physics.OverlapSphere(transform.position, detectieRadius, xrOriginLayer);
        bool result = false;
        string blockerMsg = "";

        foreach (Collider origin in xrOrigins)
        {
            RaycastHit hit;
            if (HasLineOfSight(origin.transform, out hit))
            {
                result = true;
                break;
            }
            blockerMsg = $"{hit.transform.name} (Layer {hit.transform.gameObject.layer})";
        }

        laatsteLOSKipPos = transform.position;
        laatsteLOSHunterPos = huidigeHunterPos;
        laatsteLOSResultaat = result;

        if (result != vorigeLOS)
        {
            string statusMsg = result
                ? "ZIET HEM — vrij zicht"
                : $"ZIET HEM NIET — geblokkeerd door: {blockerMsg}";
            Debug.Log($"[KANZIEN] Status: {statusMsg} | {xrOrigins.Length} colliders | " +
                      $"kip: {transform.position:F1} | hunter: {(xrOrigin != null ? xrOrigin.position.ToString("F1") : "?")}");
            vorigeLOS = result;
        }

        return result;
    }

    private bool HasLineOfSight(Transform target, out RaycastHit hitInfo)
    {
        Vector3 startPositie = transform.position + Vector3.up * 0.5f;
        Vector3 targetPositie = target.position + Vector3.up * 0.5f;
        Vector3 direction = targetPositie - startPositie;
        float afstand = direction.magnitude;

        if (Physics.Raycast(startPositie, direction.normalized, out hitInfo, afstand, obstaclesLayer, QueryTriggerInteraction.Collide))
        {
            Debug.Log($"[LOS] BLOK: {hitInfo.transform.name} (Layer {hitInfo.transform.gameObject.layer}) " +
                      $"op afstand {hitInfo.distance:F2}m " +
                      $"| start:{startPositie:F2} → eind:{targetPositie:F2}");
            Debug.DrawLine(startPositie, hitInfo.point, Color.red, 5f);
            return false;
        }

        Debug.DrawLine(startPositie, targetPositie, Color.green, 5f);
        return true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDead) return;

        if (collision.gameObject.CompareTag("Bullet"))
        {
            PlayRandomDeathSound();
            PlayDeathEffect();
            AddReward(-20.0f);
            TriggerLevelReset();
        }
    }

    private void TriggerLevelReset()
    {
        isDead = true;

        // Verberg de kip en zet colliders/physics uit zodat de verbinding met Python open blijft
        if (col != null) col.enabled = false;
        foreach (Renderer r in renderers)
        {
            if (r != null) r.enabled = false;
        }
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
        }

        LevelSpawner spawner = GetComponentInParent<LevelSpawner>();
        if (spawner != null)
        {
            spawner.CheckAllChickensDeadAndReset();
        }
    }

    public void Revive(Vector3 spawnPosition)
    {
        isDead = false;

        // Schakel visuals en colliders weer in
        if (col != null) col.enabled = true;
        foreach (Renderer r in renderers)
        {
            if (r != null) r.enabled = true;
        }
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
        }

        transform.position = spawnPosition;

        // Reset de episode voor ML-Agents
        EndEpisode();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Snack"))
        {
            if (huidigeHonger < maxHonger - 5)
            {
                other.gameObject.SetActive(false);
                huidigeHonger = Mathf.Min(huidigeHonger + hongerHerstelPerSnack, maxHonger);

                // Beloning schaalt met honger: volle kip krijgt bijna niks, hongerige kip krijgt volle reward
                float hongerDeficit = 1f - (huidigeHonger / maxHonger);
                AddReward(1.5f * hongerDeficit);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectieRadius);

        if (Application.isPlaying && rb != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }

    private void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 18;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        float hungerPct = maxHonger > 0 ? huidigeHonger / maxHonger : 0;
        float distance = xrOrigin != null ? Vector3.Distance(transform.position, xrOrigin.position) : 0;
        bool zietSpeler = Application.isPlaying && xrOrigin != null ? KanXROriginZien() : false;

        GUI.Label(new Rect(20, 20, 400, 30), $"Honger: {huidigeHonger:F1} / {maxHonger} ({hungerPct * 100:F1}%)", style);
        GUI.Label(new Rect(20, 50, 400, 30), $"Afstand tot speler: {distance:F2}m", style);

        GUIStyle losStyle = new GUIStyle(style);
        losStyle.normal.textColor = zietSpeler ? Color.green : Color.red;
        GUI.Label(new Rect(20, 80, 400, 30), $"Ziet speler: {(zietSpeler ? "JA" : "Nee")}", losStyle);

        float kipDelta = Vector3.Distance(transform.position, laatsteLOSKipPos);
        float hunterDelta = Vector3.Distance(xrOrigin != null ? xrOrigin.position : Vector3.zero, laatsteLOSHunterPos);
        bool isCached = kipDelta < losCacheAfstand && hunterDelta < losCacheAfstand;
        GUI.Label(new Rect(20, 110, 400, 30), $"Cache: {(isCached ? "HIT" : "MISS")} (kipΔ={kipDelta:F2}, hunterΔ={hunterDelta:F2})", style);

        GUI.Label(new Rect(20, 140, 400, 30), $"Reward: {GetCumulativeReward():F2}", style);

        if (hungerPct < 0.3f)
        {
            GUIStyle warningStyle = new GUIStyle(style);
            warningStyle.normal.textColor = Color.red;
            GUI.Label(new Rect(20, 170, 400, 30), "⚠ HONGERING!", warningStyle);
        }
    }
    private void PlayRandomDeathSound()
    {
        if (deathSounds != null && deathSounds.Length > 0)
        {
            int randomIndex = Random.Range(0, deathSounds.Length);
            AudioClip clipToPlay = deathSounds[randomIndex];

            PlayCustom3DSound(clipToPlay, transform.position, audioMinRadius, audioMaxRadius);
        }
    }

    private void PlayCustom3DSound(AudioClip clip, Vector3 position, float minDistance, float maxDistance)
    {
        // Maak een tijdelijk onzichtbaar object aan in de ruimte
        GameObject tempAudioObject = new GameObject("Temp3DAudio");
        tempAudioObject.transform.position = position;

        // Voeg een AudioSource toe en koppel het geluidsbestand
        AudioSource audioSource = tempAudioObject.AddComponent<AudioSource>();
        audioSource.clip = clip;

        // Zet Spatial Blend op 1.0 voor VOLLEDIG 3D ruimtelijk geluid
        audioSource.spatialBlend = 1.0f;

        // Hier stellen we jouw aangepaste radius in!
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;

        // Linear zorgt ervoor dat het geluid heel natuurlijk en gelijkmatig wegfadet
        audioSource.rolloffMode = AudioRolloffMode.Linear;

        // Speel het geluid af
        audioSource.Play();

        // Verwijder het tijdelijke object automatisch zodra het geluidsfragment is afgelopen
        Destroy(tempAudioObject, clip.length);
    }
    private void PlayDeathEffect()
    {
        if (featherExplosionPrefab != null)
        {
            // Spawn de explosie op de huidige positie van de kip
            GameObject explosion = Instantiate(featherExplosionPrefab, transform.position, Quaternion.identity);

            // Vernietig het particle object na 3 seconden om het geheugen schoon te houden
            Destroy(explosion, 3f);
        }
    }
}