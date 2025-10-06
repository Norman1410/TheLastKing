using System.Collections;
using UnityEngine;

public class BlinkingObstacle : MonoBehaviour
{
    [Header("Ciclo")]
    [SerializeField] private float tiempoEncendido = 2f; // Visible/activo
    [SerializeField] private float tiempoApagado = 2f;   // Oculto/inactivo
    [SerializeField] private bool iniciarActivo = true;
    [SerializeField] private float retrasoInicial = 0f;  // Opcional

    [Header("Qué desactivar")]
    [Tooltip("Si está en true, afectará también a todos los hijos.")]
    [SerializeField] private bool incluirHijos = true;

    private Renderer[] _renderers;
    private Collider[] _colliders;
    private Behaviour[] _extraBehaviours; // por si quieres apagar scripts extra
    private Coroutine _loop;

    private void Awake()
    {
        // Cachear componentes
        if (incluirHijos)
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
        }
        else
        {
            _renderers = GetComponents<Renderer>();
            _colliders = GetComponents<Collider>();
        }
    }

    private void OnEnable()
    {
        _loop = StartCoroutine(BlinkLoop());
    }

    private void OnDisable()
    {
        if (_loop != null) StopCoroutine(_loop);
    }

    private IEnumerator BlinkLoop()
    {
        if (retrasoInicial > 0f) yield return new WaitForSeconds(retrasoInicial);

        bool activo = iniciarActivo;
        AplicarEstado(activo);

        while (true)
        {
            yield return new WaitForSeconds(activo ? tiempoEncendido : tiempoApagado);
            activo = !activo;
            AplicarEstado(activo);
        }
    }

    private void AplicarEstado(bool activo)
    {
        // Mostrar/ocultar (visual)
        if (_renderers != null)
            foreach (var r in _renderers) if (r) r.enabled = activo;

        // Habilitar/deshabilitar colisión
        if (_colliders != null)
            foreach (var c in _colliders) if (c) c.enabled = activo;
    }
}
