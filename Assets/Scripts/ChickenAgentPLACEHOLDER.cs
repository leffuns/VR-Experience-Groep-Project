using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class ChickenAgent : Agent
{
    public float moveSpeed = 5f;
    public Transform hunter;

    public override void OnActionReceived(ActionBuffers actions)
    {
        // 1. Movement Logic (Discrete or Continuous)
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        transform.Translate(new Vector3(moveX, 0, moveZ) * Time.deltaTime * moveSpeed);

        // 2. The "Fear" Reward
        float distanceToHunter = Vector3.Distance(transform.position, hunter.position);
        
        if (distanceToHunter < 2.0f) {
            AddReward(-1.0f); // Very bad! Caught/Too close.
            EndEpisode();
        } else {
            // Reward staying away, but small so it doesn't just run to a corner forever
            AddReward(0.01f); 
        }

        // 3. The "Hiding" Reward
        // Use a Raycast to see if the Hunter's view is blocked by an "Obstacle"
        Vector3 directionToHunter = hunter.position - transform.position;
        if (Physics.Raycast(transform.position, directionToHunter, out RaycastHit hit, distanceToHunter))
        {
            if (hit.collider.CompareTag("Obstacle")) {
                AddReward(0.05f); // Good chicken! You are hidden.
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Manual control for testing (WASD)
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");
    }
}