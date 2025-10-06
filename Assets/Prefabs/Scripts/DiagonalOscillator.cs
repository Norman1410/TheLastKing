using UnityEngine;

public class DiagonalOscillator : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private Vector3 direccion = new Vector3(1f, 1f, 0f); // Diagonal por defecto
    [SerializeField] private float amplitud = 2f;       // Distancia máxima desde el pivote
    [SerializeField] private float velocidad = 1f;      // Ciclos por segundo
    [SerializeField] private float fase = 0f;           // Desfase para desincronizar con otros
    [SerializeField] private bool usarEspacioLocal = false;

    private Vector3 _pivote;
    private Vector3 _dirNorm;

    private void OnValidate()
    {
        _dirNorm = direccion.sqrMagnitude > 0f ? direccion.normalized : Vector3.right;
    }

    private void Start()
    {
        _pivote = usarEspacioLocal ? transform.localPosition : transform.position;
        _dirNorm = direccion.sqrMagnitude > 0f ? direccion.normalized : Vector3.right;
    }

    private void Update()
    {
        float offset = Mathf.Sin((Time.time + fase) * Mathf.PI * 2f * velocidad) * amplitud;
        Vector3 destino = _pivote + _dirNorm * offset;

        if (usarEspacioLocal)
            transform.localPosition = destino;
        else
            transform.position = destino;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 dir = direccion.sqrMagnitude > 0f ? direccion.normalized : Vector3.right;

        Vector3 basePos = usarEspacioLocal ? transform.parent ? transform.parent.TransformPoint(transform.localPosition) : transform.position : transform.position;
        Vector3 center = Application.isPlaying ? (usarEspacioLocal ? transform.parent ? transform.parent.TransformPoint(_pivote) : _pivote : _pivote) : basePos;

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(center - dir * amplitud, center + dir * amplitud);
        Gizmos.DrawSphere(center + dir * amplitud, 0.08f);
        Gizmos.DrawSphere(center - dir * amplitud, 0.08f);
    }
}
