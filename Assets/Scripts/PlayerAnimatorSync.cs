using UnityEngine;
using Unity.Netcode;

public class PlayerAnimatorSync : NetworkBehaviour
{
    // Owner puede escribir, todos leen
    // Variables de red para sincronizar el estado de los parámetros del Animator
    private NetworkVariable<float> netSpeed = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    
    private NetworkVariable<float> netDirection = new NetworkVariable<float>(
    0f,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Owner);


    private NetworkVariable<bool> netIsRunning = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private NetworkVariable<bool> netIsJumping = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private NetworkVariable<bool> netIsGrounded = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private Animator animator;

    // umbral para no escribir a la red cada frame inútilmente
    private const float SPEED_THRESHOLD = 0.03f;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator == null) animator = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        // Todos los clientes se suscriben para aplicar cambios localmente
        netSpeed.OnValueChanged += OnSpeedChanged;
        netDirection.OnValueChanged += OnDirectionChanged;
        netIsRunning.OnValueChanged += OnIsRunningChanged;
        netIsJumping.OnValueChanged += OnIsJumpingChanged;
        netIsGrounded.OnValueChanged += OnIsGroundedChanged;

        // Aplicar los valores actuales al animator (por si ya vienen con datos)
        ApplyAllToAnimator();
    }

    public override void OnNetworkDespawn()
    {
        netSpeed.OnValueChanged -= OnSpeedChanged;
        netDirection.OnValueChanged -= OnDirectionChanged;
        netIsRunning.OnValueChanged -= OnIsRunningChanged;
        netIsJumping.OnValueChanged -= OnIsJumpingChanged;
        netIsGrounded.OnValueChanged -= OnIsGroundedChanged;
    }

    // -------------------------
    // Owner calls (write ops)
    // -------------------------
    public void OwnerSetSpeed(float newSpeed)
    {
        if (!IsOwner) return;

        // sólo escribir si hay cambio significativo
        if (Mathf.Abs(netSpeed.Value - newSpeed) > SPEED_THRESHOLD)
            netSpeed.Value = newSpeed;
    }

    public void OwnerSetDirection(float direction)
    {
        if (!IsOwner) return;

        // Evita mandar valores idénticos para reducir tráfico
        if (Mathf.Abs(netDirection.Value - direction) > 0.01f)
            netDirection.Value = direction;
    }



    public void OwnerSetRunning(bool run)
    {
        if (!IsOwner) return;
        if (netIsRunning.Value != run) netIsRunning.Value = run;
    }

    public void OwnerSetJumping(bool jump)
    {
        if (!IsOwner) return;
        if (netIsJumping.Value != jump) netIsJumping.Value = jump;
    }

    public void OwnerSetGrounded(bool grounded)
    {
        if (!IsOwner) return;
        if (netIsGrounded.Value != grounded) netIsGrounded.Value = grounded;
    }

    // -------------------------
    // Callbacks: apply locally
    // -------------------------
    private void OnSpeedChanged(float previous, float current) => animator.SetFloat("Speed", current);
    private void OnDirectionChanged(float previous, float current) => animator.SetFloat("Direction", current);

    private void OnIsRunningChanged(bool previous, bool current) => animator.SetBool("IsRunning", current);
    private void OnIsJumpingChanged(bool previous, bool current) => animator.SetBool("IsJumping", current);
    private void OnIsGroundedChanged(bool previous, bool current) => animator.SetBool("IsGrounded", current);

    private void ApplyAllToAnimator()
    {
        animator.SetFloat("Speed", netSpeed.Value);
        animator.SetFloat("Direction", netDirection.Value);
        animator.SetBool("IsRunning", netIsRunning.Value);
        animator.SetBool("IsJumping", netIsJumping.Value);
        animator.SetBool("IsGrounded", netIsGrounded.Value);
    }
}
