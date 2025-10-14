using UnityEngine;

public class VariableSpeedPlatformEaseInOut : MonoBehaviour
{
    public enum Espacio { Mundo, Local }

    [Header("Ruta (A/B opcional)")]
    [Tooltip("Si se asignan A y B, se ignoran 'direccion' y 'distancia'.")]
    [SerializeField] private Transform puntoA;
    [SerializeField] private Transform puntoB;

    [Tooltip("Usado si no se asignan A y B.")]
    [SerializeField] private Vector3 direccion = Vector3.right;
    [SerializeField] private float distancia = 6f;

    [Header("Curva (0→1)")]
    [SerializeField] private AnimationCurve curva = null; // se inicializa en Reset
    [SerializeField] private float duracion = 3f; // tiempo de un recorrido de 0→1
    [SerializeField] private Espacio espacio = Espacio.Mundo;
    [SerializeField] private float fase = 0f; // desfase en segundos

    private Vector3 _a, _b;
    private float _t;

    private void Reset()
    {
        // Curva EaseInOut por defecto
        curva = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        direccion = Vector3.right;
        distancia = 6f;
        duracion = 3f;
        espacio = Espacio.Mundo;
        fase = 0f;
    }

    private void Start()
    {
        if (puntoA && puntoB)
        {
            _a = puntoA.position;
            _b = puntoB.position;
        }
        else
        {
            var basePos = espacio == Espacio.Mundo ? transform.position : transform.localPosition;
            var dir = direccion.sqrMagnitude > 0 ? direccion.normalized : Vector3.right;
            _a = basePos;
            _b = basePos + dir * distancia;
        }
        _t = fase;
    }

    private void Update()
    {
        if (duracion <= 0f || curva == null) return;
        _t += Time.deltaTime;

        // Recorre 0→1 en bucle
        float u = Mathf.Repeat(_t / duracion, 1f);

        // EaseInOut típico: va 0→1 (y al repetir, salta a 0 de nuevo)
        float k = Mathf.Clamp01(curva.Evaluate(u));
        Vector3 destino = Vector3.LerpUnclamped(_a, _b, k);

        if (espacio == Espacio.Mundo) transform.position = destino;
        else transform.localPosition = destino;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 a, b;
        if (puntoA && puntoB) { a = puntoA.position; b = puntoB.position; }
        else
        {
            var basePos = Application.isPlaying ? _a : transform.position;
            var dir = direccion.sqrMagnitude > 0 ? direccion.normalized : Vector3.right;
            a = basePos; b = basePos + dir * distancia;
        }
        Gizmos.DrawLine(a, b);
        Gizmos.DrawSphere(a, 0.07f);
        Gizmos.DrawSphere(b, 0.07f);
    }
}
