using UnityEngine;
using System.Collections;

public class SpringTrapController : MonoBehaviour
{
    // Variables para asignar en el Inspector
    public Rigidbody hatchDoorRigidbody; // Asigna el Rigidbody de Hatch_Door
    public Transform springPadTransform;  // Asigna el Transform de Spring_Pad
    public float launchForce = 15f;      // Fuerza de lanzamiento del personaje
    public float springDuration = 0.2f;  // Tiempo que tarda el resorte en subir
    public float resetDelay = 3f;        // Tiempo antes de que la trampa se reinicie

    private Vector3 springStartPos;
    private Vector3 springLaunchPos;
    private bool isReady = true;

    void Start()
    {
        // Posiciones del resorte
        springStartPos = springPadTransform.localPosition;
        
        // Define la posición máxima de lanzamiento (por encima de donde estaba la puerta)
        springLaunchPos = springStartPos + Vector3.up * 1.5f; 

        // Inicialmente, la puerta no debe caer
        hatchDoorRigidbody.isKinematic = true; 
    }

    // Se activa cuando el jugador pisa el sensor Trigger
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && isReady)
        {
            isReady = false; // Bloquea la trampa
            StartCoroutine(ActivateTrap(other.gameObject));
        }
    }

    IEnumerator ActivateTrap(GameObject player)
    {
        // 1. Abrir la Escotilla: Desactiva Kinematic para que la puerta caiga por gravedad
        hatchDoorRigidbody.isKinematic = false;
        
        // Espera un instante para que la puerta caiga y el jugador caiga al Spring Pad
        yield return new WaitForSeconds(0.1f); 

        // 2. Activar el Resorte: Lo mueve rápidamente hacia la posición de lanzamiento
        float t = 0;
        Vector3 currentPos = springPadTransform.localPosition;

        while (t < 1)
        {
            t += Time.deltaTime / springDuration;
            springPadTransform.localPosition = Vector3.Lerp(currentPos, springLaunchPos, t);
            yield return null;
        }

        // 3. Aplicar Fuerza de Lanzamiento al Jugador
        // NOTA: Asegúrate que el player tenga un Rigidbody para que esto funcione
        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            // Aplica la fuerza hacia arriba. ForceMode.VelocityChange ignora la masa
            playerRb.AddForce(Vector3.up * launchForce, ForceMode.VelocityChange); 
        }

        // 4. Reiniciar el Resorte: Vuelve a la posición inicial
        yield return new WaitForSeconds(0.5f); // Pausa visual
        t = 0;
        currentPos = springPadTransform.localPosition;

        while (t < 1)
        {
            t += Time.deltaTime / springDuration;
            springPadTransform.localPosition = Vector3.Lerp(currentPos, springStartPos, t);
            yield return null;
        }

        // 5. Esperar el Reinicio de la Tapa (simulando que un mecanismo la devuelve)
        yield return new WaitForSeconds(resetDelay);
        ResetTrapDoor();
    }

    private void ResetTrapDoor()
    {
        // Coloca la tapa de nuevo en su posición inicial y la fija
        hatchDoorRigidbody.transform.localPosition = Vector3.zero; // Vuelve a la posición inicial de la trampa
        hatchDoorRigidbody.transform.localRotation = Quaternion.identity;
        hatchDoorRigidbody.isKinematic = true;
        isReady = true;
    }
}
