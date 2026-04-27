using UnityEngine;

// Zorg ervoor dat het object een Collider en Rigidbody heeft
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class VRBullet : MonoBehaviour
{
    // Hoe lang de kogel blijft bestaan als hij niks raakt
    public float selfDestructTime = 5.0f;

    void Start()
    {
        // Vernietig de kogel automatisch na X seconden om lag te voorkomen
        Destroy(gameObject, selfDestructTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Jouw groepsgenoot heeft de kip geprogrammeerd om een Tag-check te doen.
        // We hoeven hier dus niks speciaals te doen, behalve zorgen dat deze kogel verdwijnt.

        // Optioneel: Maak hier een impact effect (pluim van veren, etc.)

        // Vernietig de kogel zodra hij IETS raakt (muur, vloer, of kip)
        Destroy(gameObject);
    }
}