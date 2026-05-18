using UnityEngine;

// Zorg ervoor dat het object een Collider en Rigidbody heeft
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class VRBullet : MonoBehaviour
{
    // Hoe lang de kogel blijft bestaan als hij niks raakt
    public float selfDestructTime = 5.0f;

    [Header("Impact Effect")]
    public GameObject sparkPrefab;

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
        if (!collision.gameObject.CompareTag("Chicken"))
        {
            if (sparkPrefab != null)
            {
                // 1. Pak het exacte punt waar de kogel de muur raakt
                ContactPoint contact = collision.contacts[0];

                // 2. Bereken de rotatie: de vonken moeten van de muur AF vliegen (de "normal" richting)
                Quaternion hitRotation = Quaternion.LookRotation(contact.normal);

                // 3. Spawn de vonken op dat exacte punt met de juiste richting
                GameObject sparks = Instantiate(sparkPrefab, contact.point, hitRotation);

                // 4. Ruim de vonken na 1 seconde netjes op
                Destroy(sparks, 1.0f);
            }
        }

        // Vernietig de kogel zodra hij IETS raakt (muur, vloer, of kip)
        Destroy(gameObject);
    }
}