using UnityEngine;

public class VariableSpeedPlatform : MonoBehaviour
{
    public enum ModoCiclo { Loop, PingPong }
    public enum Espacio { Mundo, Local }

    [Header("Ruta")]
    [SerializeField] private Transform puntoA;
    [SerializeField] private Transform puntoB;

    [SerializeField] private Vector3 direccion = Vector3.right;
    [SerializeField] private float distancia = 6f;

    [Header("Curva de posición (0→1)")]
    [SerializeField] private AnimationCurve curva;
    [SerializeField] private float duracion = 3f;
    [SerializeField] private ModoCiclo modoCiclo = ModoCiclo.PingPong;
    [SerializeField] private Espacio espacio = Espacio.Mundo;
    [SerializeField] private float fase = 0f;

    private Vector3 _a, _b;
    private float _t;

    private void Reset()
    {
        // Configuración por defecto: EaseInOut (lento → rápido → lento)
        curva = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f),   // empieza lento
            new Keyframe(0.5f, 1f, 0f, 0f), // acelera en medio
            new Keyframe(1f, 0f, -2f, 0f)   // frena al final
        );

        direccion = Vector3.right;
        distancia = 6f;
        duracion = 3f;
        modoCiclo = ModoCiclo.PingPong;
        espacio = Espacio.Mundo;
    }

    private void Start()
    {
        if (puntoA != null && puntoB != null)
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
        if (duracion <= 0f) return;
        _t += Time.deltaTime;

        float u = Mathf.Repeat(_t / duracion, 1f);
        if (modoCiclo == ModoCiclo.PingPong)
            u = Mathf.PingPong(_t / duracion, 1f);

        float k = Mathf.Clamp01(curva.Evaluate(u));
        Vector3 destino = Vector3.LerpUnclamped(_a, _b, k);

        if (espacio == Espacio.Mundo) transform.position = destino;
        else transform.localPosition = destino;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(_a, _b);
        Gizmos.DrawSphere(_a, 0.07f);
        Gizmos.DrawSphere(_b, 0.07f);
    }
}
