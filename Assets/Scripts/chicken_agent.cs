using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;


/*
    1.	Space Size Aanpassen: We hebben zojuist de hongerwaarde toegevoegd aan CollectObservations. Je moet nu de Space Size in je Behavior Parameters component op de kip veranderen van 7 naar 8. Doe je dit niet, dan crasht ML-Agents meteen.
	2.	Snacks Instellen: Selecteer al je cola-blikjes en kippennuggets in de scene. Verander hun Tag naar Snack (maak deze tag aan als hij nog niet bestaat, met een hoofdletter S!).
	3.	Triggers Maken: Zorg dat de Colliders op je snacks staan aangevinkt als Is Trigger. Als je dit vergeet, botst de kip ertegenaan alsof het stenen zijn.
	4.	De Array Vullen: Sleep al je snacks vanuit je scene naar de Snacks array op het script van de kip in de Inspector. Zo weet de kip welke objecten hij bij het begin van elke ronde opnieuw zichtbaar moet maken.
*/


[RequireComponent(typeof(Rigidbody))]
public class chicken_agent : Agent
{
    [Header("Doelwit & Beweging")]
    public Transform shooter;
    public float moveSpeed = 5f;
    
    [Header("Raycast Instellingen")]
    public float zichtReikwijdte = 25f;
    public LayerMask obstaclesLayer;

    [Header("Honger & Snacks (Kannibalisme?!)")]
    public float maxHonger = 100f;
    [Tooltip("Hoeveel honger er per stap (OnActionReceived) afgaat.")]
    public float hongerAfnamePerStap = 0.1f;
    public float hongerHerstelPerSnack = 40f;
    [Tooltip("Sleep je cola en nuggets hierin zodat het script ze kan resetten.")]
    public GameObject[] snacks; 
    
    private float huidigeHonger;
    private Rigidbody rb;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        rb.linearVelocity = Vector3.zero;
        
        // Vul de maag weer bij de start
        huidigeHonger = maxHonger;

        // Reset alle snacks zodat ze weer in de scene staan voor de nieuwe episode
        foreach (GameObject snack in snacks)
        {
            if (snack != null) snack.SetActive(true);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(shooter.localPosition);
        sensor.AddObservation(KanSchutterZien() ? 1f : 0f);
        
        // De kip moet weten hoe hongerig hij is. 
        // Genormaliseerd (tussen 0 en 1) is beter voor het neurale netwerk!
        sensor.AddObservation(huidigeHonger / maxHonger);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];

        Vector3 move = new Vector3(moveX, 0, moveZ).normalized;
        rb.AddForce(move * moveSpeed);

        // Honger systeem verwerken
        huidigeHonger -= hongerAfnamePerStap;
        
        if (huidigeHonger <= 0)
        {
            // Uitgehongerd = dood. Flinke straf, net als bij een kogel!
            AddReward(-1.0f);
            EndEpisode();
            return; // Stop verdere berekeningen in deze stap
        }

        if (KanSchutterZien())
        {
            AddReward(-0.01f); 
        }
        else
        {
            AddReward(0.01f); 
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");
    }

    private bool KanSchutterZien()
    {
        if (shooter == null) return false;

        Vector3 startPositie = transform.position + Vector3.up * 0.5f;
        Vector3 richting = (shooter.position + Vector3.up * 0.5f) - startPositie;

        RaycastHit hit;
        
        if (Physics.Raycast(startPositie, richting.normalized, out hit, zichtReikwijdte, ~obstaclesLayer))
        {
            if (hit.transform == shooter)
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
            AddReward(-1.0f);
            EndEpisode();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Controleer of de kip een snack aanraakt
        if (other.CompareTag("Snack"))
        {
            // Eet het op (verdwijnt uit de scene)
            other.gameObject.SetActive(false);
            
            // Herstel honger, maar ga niet over de maximale limiet heen
            huidigeHonger = Mathf.Min(huidigeHonger + hongerHerstelPerSnack, maxHonger);
            
            // Geef een flinke beloning voor het zoeken van voedsel!
            AddReward(0.5f);
        }
    }
}