using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class SpringTrapController : MonoBehaviour
{
    // Variables para asignar en el Inspector
    public Rigidbody hatchDoorRigidbody; // Asigna el Rigidbody de Hatch_Door
    public Transform springPadTransform;  // Asigna el Transform de Spring_Pad
    public float launchForce = 15f;      // Fuerza de lanzamiento del personaje
    public float springDuration = 0.2f;  // Tiempo que tarda el resorte en subir
    public float resetDelay = 3f;        // Tiempo antes de que la trampa se reinicie

    [Header("Crown Launch Settings")]
    [Tooltip("Direction the crown flies in world space (e.g., (0, 1, 5) for Up and Forward)")]
    public Vector3 crownLaunchDirection = new Vector3(0f, 1f, 5f);
    [Tooltip("Force for the crown launch.")]
    public float crownLaunchForce = 15f;


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

        // Obtener la referencia al script de movimiento del jugador UNA VEZ
        // (Tu script se llama FirstPersonController)
        FirstPersonController fpc = player.GetComponent<FirstPersonController>();

        // ----------------------------------------------------
        // CROWN DROP LOGIC (NETWORKED FIX)
        // ----------------------------------------------------
        PlayerRob pr = player.GetComponent<PlayerRob>();
        if (pr != null && pr.HasCrown())
        {
            // 1. Calculate the final direction the crown will fly based on trap rotation
            Vector3 worldLaunchDirection = transform.TransformDirection(crownLaunchDirection.normalized);

            // 2. Call the Server RPC function you added to PlayerRob.cs.
            // This starts the chain: Client -> Server (State Change) -> All Clients (Physics Drop)
            pr.DropCrownServerRpc(worldLaunchDirection, crownLaunchForce);
            
            Debug.Log($"[SpringTrap] Player {player.name} requested crown drop via ServerRpc.");
        }
        // ----------------------------------------------------

        // Espera un instante para que la puerta caiga y el jugador caiga al Spring Pad
        yield return new WaitForSeconds(0.1f);

        // Espera un instante para que la puerta caiga y el jugador caiga al Spring Pad
        yield return new WaitForSeconds(0.1f);

        // 2. Activar el Resorte: Lo mueve rápidamente hacia la posición de lanzamiento
        float t = 0;
        Vector3 currentPos = springPadTransform.localPosition;

        while (t < 1)
        {
            t += Time.deltaTime / springDuration;
            springPadTransform.localPosition = Vector3.Lerp(currentPos, springLaunchPos, t);

            // *** LÓGICA DE LANZAMIENTO INTEGRADA EN EL MOVIMIENTO DEL RESORTE ***
            if (fpc != null)
            {
                // Llama a la función pública en tu script FirstPersonController.cs
                // para sobrescribir la velocidad vertical.
                fpc.ApplyExternalLaunch(launchForce);

                // Usamos 'launchForce' como velocidad (m/s).
                // Tu FPC gestiona la velocidad; no uses Time.deltaTime aquí, pues ApplyExternalLaunch
                // solo actualiza la variable 'velocity.y' y la gravedad hará el resto.
            }
            else
            {
                // Alternativa para Rigidbody (aunque sabemos que el jugador no lo tiene)
                Rigidbody playerRb = player.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    playerRb.AddForce(Vector3.up * launchForce, ForceMode.VelocityChange);
                }
            }
            // ********************************************************************

            yield return null;
        }

        // (La Sección 3 anterior fue eliminada, ya que la lógica se movió al bucle anterior)
        

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
        
        // **AÑADIDO:** Coloca la tapa inmediatamente para que el tiempo de espera no se vea mal
        hatchDoorRigidbody.transform.localPosition = Vector3.zero;

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
