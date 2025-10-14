using UnityEngine;

public class CircularMover : MonoBehaviour
{
    [Header("Movimiento circular")]
    [SerializeField] private float radioX = 2f;   // Radio en eje X
    [SerializeField] private float radioY = 2f;   // Radio en eje Y
    [SerializeField] private float velocidad = 1f; // Revoluciones por segundo
    [SerializeField] private bool usarEspacioLocal = false;

    private Vector3 _pivote;

    private void Start()
    {
        _pivote = usarEspacioLocal ? transform.localPosition : transform.position;
    }

    private void Update()
    {
        float angulo = Time.time * velocidad * Mathf.PI * 2f;
        float x = Mathf.Cos(angulo) * radioX;
        float y = Mathf.Sin(angulo) * radioY;

        Vector3 destino = _pivote + new Vector3(x, y, 0f);

        if (usarEspacioLocal)
            transform.localPosition = destino;
        else
            transform.position = destino;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        const int steps = 40;
        Vector3 prev = Vector3.zero;

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps * Mathf.PI * 2f;
            Vector3 point = new Vector3(Mathf.Cos(t) * radioX, Mathf.Sin(t) * radioY, 0f);
            if (i > 0) Gizmos.DrawLine(_pivote + prev, _pivote + point);
            prev = point;
        }
    }
}
