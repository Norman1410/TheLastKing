using UnityEngine;

public class RotatingObstacle : MonoBehaviour
{
    [Header("Rotación")]
    [SerializeField] private Vector3 eje = Vector3.up; // Eje de rotación (Y = vertical)
    [SerializeField] private float velocidad = 90f;    // Grados por segundo
    [SerializeField] private Space espacio = Space.World; // Mundo o local

    private void Update()
    {
        transform.Rotate(eje.normalized * velocidad * Time.deltaTime, espacio);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, eje.normalized);
    }
}