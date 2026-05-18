using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic; // Toegevoegd voor handige sorteerfuncties

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

    [Header("Honger & Snacks (Kannibalisme?!)")]
    public float maxHonger = 500f;
    [Tooltip("Hoeveel honger er per stap (OnActionReceived) afgaat.")]
    public float hongerAfnamePerStap = 0.1f;
    public float hongerHerstelPerSnack = 40f;
    [Tooltip("Sleep je cola en nuggets hierin zodat het script ze kan resetten.")]
    public GameObject[] snacks;

    [HideInInspector]
    public bool isDead = false;

    private float huidigeHonger;
    private Rigidbody rb;
    private Vector3 targetDirection;
    private Collider col;
    private Renderer[] renderers;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        col = GetComponent<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
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
            sensor.AddObservation(Vector3.zero); // 3
            sensor.AddObservation(Vector3.zero); // 3
            sensor.AddObservation(0f);           // 1
            sensor.AddObservation(0f);           // 1
            return;
        }

        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(xrOrigin.localPosition);
        sensor.AddObservation(KanXROriginZien() ? 1f : 0f);

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

        // Controleer of de kip van de rand is gevlogen/gevallen
        if (transform.position.y < -1.0f)
        {
            PlayRandomDeathSound();
            AddReward(-10.0f); // Flinke straf voor vallen!
            TriggerLevelReset();
            return;
        }

        Debug.Log("[OnActionReceived] Called!");

        // Direct input as primary source
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        Debug.Log($"[Input] Direct - X: {moveX}, Z: {moveZ}");

        // Fallback to ML-Agent actions if no input
        if (moveX == 0 && moveZ == 0)
        {
            moveX = actions.ContinuousActions[0];
            moveZ = actions.ContinuousActions[1];
            Debug.Log($"[Actions] ML-Agent fallback - X: {moveX}, Z: {moveZ}");
        }

        Debug.Log($"[OnActionReceived] Final Actions - X: {moveX}, Z: {moveZ}");

        targetDirection = new Vector3(moveX, 0, moveZ).normalized;

        Debug.Log($"[Movement] Move direction: {targetDirection}, Speed: {targetDirection * moveSpeed}");

        // Honger systeem verwerken
        huidigeHonger -= hongerAfnamePerStap;

        if (huidigeHonger <= 0)
        {
            // Uitgehongerd = dood. Flinke straf, net als bij een kogel!
            AddReward(-10.0f);
            TriggerLevelReset();
            return; // Stop verdere berekeningen in deze stap
        }

        bool zietXROrigin = KanXROriginZien();

        if (zietXROrigin)
        {
            // Dynamische angst: de straf voor gezien worden neemt af naarmate de kip hongeriger wordt.
            // Als de kip bijna uithongert (huidigeHonger is laag), is de angstfactor bijna 0,
            // waardoor hij de schuilplaats durft te verlaten om snacks te zoeken.
            float angstFactor = huidigeHonger / maxHonger;
            AddReward(-0.01f * angstFactor);
        }
        else
        {
            // Beloning voor veilig schuilen
            AddReward(0.01f);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Debug.Log($"[Heuristic] Input - Horizontal: {h}, Vertical: {v}");

        continuousActions[0] = h;
        continuousActions[1] = v;
    }

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
        Collider[] xrOrigins = Physics.OverlapSphere(transform.position, detectieRadius, xrOriginLayer);

        foreach (Collider origin in xrOrigins)
        {
            if (HasLineOfSight(origin.transform))
            {
                return true;
            }
        }
        return false;
    }

    private bool HasLineOfSight(Transform target)
    {
        Vector3 startPositie = transform.position + Vector3.up * 0.5f;
        Vector3 targetPositie = target.position + Vector3.up * 0.5f;
        Vector3 direction = targetPositie - startPositie;

        RaycastHit hit;

        if (Physics.Raycast(startPositie, direction.normalized, out hit, detectieRadius))
        {
            if (hit.transform == target || hit.transform.IsChildOf(target))
            {
                return true;
            }
        }
        return false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Bullet"))
        {
            PlayRandomDeathSound();
            AddReward(-10.0f);
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

                // Geef een flinke beloning voor het zoeken van voedsel!
                AddReward(0.5f);
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

        GUI.Label(new Rect(20, 20, 300, 30), $"Honger: {huidigeHonger:F1} / {maxHonger} ({hungerPct * 100:F1}%)", style);
        GUI.Label(new Rect(20, 50, 300, 30), $"Afstand tot speler: {distance:F2}m", style);
        GUI.Label(new Rect(20, 80, 300, 30), $"Ziet speler: {(zietSpeler ? "JA" : "Nee")}", style);
        GUI.Label(new Rect(20, 110, 300, 30), $"Reward: {GetCumulativeReward():F2}", style);

        if (hungerPct < 0.3f)
        {
            GUIStyle warningStyle = new GUIStyle(style);
            warningStyle.normal.textColor = Color.red;
            GUI.Label(new Rect(20, 140, 300, 30), "⚠ HONGERING!", warningStyle);
        }
    }
    private void PlayRandomDeathSound()
    {
        if (deathSounds != null && deathSounds.Length > 0)
        {
            int randomIndex = Random.Range(0, deathSounds.Length);
            AudioClip clipToPlay = deathSounds[randomIndex];
            
            AudioSource.PlayClipAtPoint(clipToPlay, transform.position);
        }
    }
}