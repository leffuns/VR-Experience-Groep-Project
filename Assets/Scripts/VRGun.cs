using UnityEngine;
using UnityEngine.InputSystem; // Added this to read VR controllers directly

public class VRGun : MonoBehaviour
{
    [Header("Settings")]
    public GameObject bulletPrefab;
    public Transform spawnPoint;
    public float bulletVelocity = 30.0f;

    [Header("VR Input")]
    [Tooltip("Click the little circle to assign the Right Trigger action")]
    public InputActionReference triggerInput; 

    private void OnEnable()
    {
        // Start listening for the trigger pull when the gun is active
        if (triggerInput != null)
        {
            triggerInput.action.Enable();
            triggerInput.action.performed += ContextShoot;
        }
    }

    private void OnDisable()
    {
        // Stop listening to prevent memory leaks
        if (triggerInput != null)
        {
            triggerInput.action.performed -= ContextShoot;
        }
    }

    // This converts the Input System event into your standard Shoot() function
    private void ContextShoot(InputAction.CallbackContext context)
    {
        Shoot();
    }

    public void Shoot()
    {
        if (bulletPrefab == null || spawnPoint == null)
        {
            Debug.LogError("VRGun: Missing Prefab or SpawnPoint!");
            return;
        }

        GameObject projectile = Instantiate(bulletPrefab, spawnPoint.position, spawnPoint.rotation);
        
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = spawnPoint.forward * bulletVelocity;
        }
        else
        {
            Debug.LogError("VRGun: The bullet prefab is missing a Rigidbody!");
        }
    }
}