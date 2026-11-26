using UnityEngine;

public class PuertaInteractuable : MonoBehaviour, IInteractable
{
    [Header("Animación")]
    public Animator animator;
    public string parametroAbierta = "Abierta";

    private bool estaAbierta = false;

    void Start()
    {
        if (animator == null)
        {
            animator = GetComponentInParent<Animator>();
        }
    }

    public void Interact()
    {
        if (animator != null)
        {
            estaAbierta = !estaAbierta;
            animator.SetBool(parametroAbierta, estaAbierta);
        }
    }
}