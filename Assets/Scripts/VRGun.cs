using UnityEngine;

public class VRGun : MonoBehaviour
{
    [Header("Instellingen")]
    public GameObject bulletPrefab;      // Sleep hier je VRBullet prefab in
    public Transform spawnPoint;         // Maak een Empty Child aan het einde van de loop
    public float bulletVelocity = 30.0f; // Snelheid van de kogel

    // Roep deze functie aan met je VR Trigger (XR Interaction Toolkit "Activated" event)
    public void Shoot()
    {
        if (bulletPrefab == null || spawnPoint == null)
        {
            Debug.LogError("VRGun: Vergeet niet de Prefab en SpawnPoint te slepen in de Inspector!");
            return;
        }

        // Maak de kogel
        GameObject projectile = Instantiate(bulletPrefab, spawnPoint.position, spawnPoint.rotation);

        // Geef hem snelheid
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = spawnPoint.forward * bulletVelocity;
        }
        else
        {
            Debug.LogError("VRGun: De kogel prefab mist een Rigidbody!");
        }
    }
}