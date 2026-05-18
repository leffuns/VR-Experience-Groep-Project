using UnityEngine;
using UnityEngine.InputSystem; 

[RequireComponent(typeof(AudioSource))] // This ensures the gun always has a speaker
public class VRGun : MonoBehaviour
{
    [Header("Settings")]
    public GameObject bulletPrefab;
    public Transform spawnPoint;
    public float bulletVelocity = 30.0f;

    [Header("Audio")]
    public AudioClip pewSound;       // Drag your sound file here
    private AudioSource audioSource; // The speaker on the gun

    [Header("VR Input")]
    public InputActionReference triggerInput; 

    private void Awake()
    {
        // Automatically find the AudioSource component on the gun
        audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        if (triggerInput != null)
        {
            triggerInput.action.Enable();
            triggerInput.action.performed += ContextShoot;
        }
    }

    private void OnDisable()
    {
        if (triggerInput != null)
        {
            triggerInput.action.performed -= ContextShoot;
        }
    }

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

        // --- PEW SOUND EFFECT door Ninky ---
        if (pewSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(pewSound); 
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