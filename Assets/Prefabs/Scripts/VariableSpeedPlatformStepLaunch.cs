using UnityEngine;

public class VariableSpeedPlatformStepLaunch : MonoBehaviour
{
    public enum Espacio { Mundo, Local }

    [Header("Ruta (A/B opcional)")]
    [SerializeField] private Transform puntoA;
    [SerializeField] private Transform puntoB;

    [SerializeField] private Vector3 direccion = Vector3.right;
    [SerializeField] private float distancia = 4f;

    [Header("Curva tipo 'salida explosiva'")]
    [SerializeField] private AnimationCurve curva = null; // se inicializa en Reset
    [SerializeField] private float duracion = 2f;
    [SerializeField] private Espacio espacio = Espacio.Mundo;
    [SerializeField] private float fase = 0f;

    private Vector3 _a, _b;
    private float _t;

    private void Reset()
    {
        // Curva con salto rápido al principio y luego suaviza
        curva = new AnimationCurve(
            new Keyframe(0.00f, 0.00f),
            new Keyframe(0.05f, 0.60f),
            new Keyframe(0.30f, 0.85f),
            new Keyframe(1.00f, 1.00f)
        );
        direccion = Vector3.right;
        distancia = 4f;
        duracion = 2f;
        espacio = Espacio.Mundo;
        fase = 0f;
    }

    private void Start()
    {
        if (puntoA && puntoB) { _a = puntoA.position; _b = puntoB.position; }
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

        // 0→1 en bucle (cada ciclo vuelve a "salir disparada")
        float u = Mathf.Repeat(_t / duracion, 1f);
        float k = Mathf.Clamp01(curva.Evaluate(u));
        Vector3 destino = Vector3.LerpUnclamped(_a, _b, k);

        if (espacio == Espacio.Mundo) transform.position = destino;
        else transform.localPosition = destino;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
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
