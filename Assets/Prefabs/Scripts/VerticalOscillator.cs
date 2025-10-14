using UnityEngine;

public class VerticalOscillator : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float amplitud = 2f;       // Distancia máxima hacia arriba/abajo
    [SerializeField] private float velocidad = 1f;      // Ciclos por segundo
    [SerializeField] private float fase = 0f;           // Desfase para desincronizar con otros
    [SerializeField] private bool usarEspacioLocal = false;

    private Vector3 _pivote;

    private void Start()
    {
        _pivote = usarEspacioLocal ? transform.localPosition : transform.position;
    }

    private void Update()
    {
        float yOffset = Mathf.Sin((Time.time + fase) * Mathf.PI * 2f * velocidad) * amplitud;
        Vector3 destino = _pivote + new Vector3(0f, yOffset, 0f);

        if (usarEspacioLocal)
            transform.localPosition = destino;
        else
            transform.position = destino;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 basePos = usarEspacioLocal ? transform.parent ? transform.parent.TransformPoint(transform.localPosition) : transform.position : transform.position;
        Vector3 center = Application.isPlaying ? (usarEspacioLocal ? transform.parent ? transform.parent.TransformPoint(_pivote) : _pivote : _pivote) : basePos;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(center + Vector3.up * amplitud, center - Vector3.up * amplitud);
        Gizmos.DrawSphere(center + Vector3.up * amplitud, 0.08f);
        Gizmos.DrawSphere(center - Vector3.up * amplitud, 0.08f);
    }
}
