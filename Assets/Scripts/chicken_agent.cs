using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
public class chicken_agent : Agent
{
    [Header("Doelwit & Beweging")]
    public Transform shooter;
    public float moveSpeed = 5f;
    
    [Header("Raycast Instellingen")]
    public float zichtReikwijdte = 25f;
    public LayerMask obstaclesLayer;

    private Rigidbody rb;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        // Zet hier logica om de positie van de kip en de schutter te resetten 
        // als de kip wordt geraakt of als de tijd om is.
        rb.linearVelocity = Vector3.zero;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // De kip moet weten waar hij is en waar de schutter is (6 waardes)
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(shooter.localPosition);

        // De kip moet weten of hij op dit moment in gevaar is (1 waarde)
        sensor.AddObservation(KanSchutterZien() ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Acties vertalen naar beweging op de X en Z as
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];

        Vector3 move = new Vector3(moveX, 0, moveZ).normalized;
        rb.AddForce(move * moveSpeed);

        // Beloningen uitdelen (Reinforcement Learning)
        if (KanSchutterZien())
        {
            // Zichtbaar zijn is slecht!
            AddReward(-0.01f); 
        }
        else
        {
            // Verstopt zijn achter een rots is goed!
            AddReward(0.01f); 
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Hiermee kun je de kip zelf besturen om te testen of de beweging werkt
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Controleer of het object dat de kip raakt de juiste Tag heeft
        if (collision.gameObject.CompareTag("Bullet"))
        {
            // Een flinke straf geven omdat de kip dood is
            AddReward(-1.0f);
            
            // Beëindig de episode direct
            EndEpisode();
        }
    }

    private bool KanSchutterZien()
    {
        if (shooter == null) return false;

        Vector3 startPositie = transform.position + Vector3.up * 0.5f;
        Vector3 richting = (shooter.position + Vector3.up * 0.5f) - startPositie;

        RaycastHit hit;
        
        // Controleer of er iets tussen de kip en de schutter staat (zoals een rots)
        if (Physics.Raycast(startPositie, richting.normalized, out hit, zichtReikwijdte, ~obstaclesLayer))
        {
            if (hit.transform == shooter)
            {
                return true; // We worden gezien!
            }
        }
        return false;
    }
}