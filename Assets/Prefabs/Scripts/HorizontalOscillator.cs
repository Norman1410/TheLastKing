using UnityEngine;

public class HorizontalOscillator : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float amplitud = 3f;      // Distancia máxima a izquierda/derecha
    [SerializeField] private float velocidad = 1f;     // Ciclos por segundo
    [SerializeField] private float fase = 0f;          // Desfase para desincronizar con otros
    [SerializeField] private bool usarEspacioLocal = false;

    private Vector3 _pivote;

    private void Start()
    {
        _pivote = usarEspacioLocal ? transform.localPosition : transform.position;
    }

    private void Update()
    {
        float zOffset = Mathf.Sin((Time.time + fase) * Mathf.PI * 2f * velocidad) * amplitud;
        Vector3 destino = _pivote + new Vector3(0f, 0f, zOffset);

        if (usarEspacioLocal)
            transform.localPosition = destino;
        else
            transform.position = destino;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 basePos = usarEspacioLocal ? transform.parent ? transform.parent.TransformPoint(transform.localPosition) : transform.position : transform.position;
        Vector3 center = Application.isPlaying ? (usarEspacioLocal ? transform.parent ? transform.parent.TransformPoint(_pivote) : _pivote : _pivote) : basePos;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(center + Vector3.right * amplitud, center - Vector3.right * amplitud);
        Gizmos.DrawSphere(center + Vector3.right * amplitud, 0.08f);
        Gizmos.DrawSphere(center - Vector3.right * amplitud, 0.08f);
    }
}
