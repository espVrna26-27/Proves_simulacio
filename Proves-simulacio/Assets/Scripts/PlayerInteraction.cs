using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Arrastra aquí la Main Camera (hija de Player)")]
    public Transform cameraTx;

    [Header("Configuración")]
    public float interactDistance = 3f;
    public LayerMask interactableMask = ~0;

    [Header("Input System")]
    public InputAction interactAction = new InputAction("Interact", InputActionType.Button, "<Mouse>/leftButton");

    void Reset()
    {
        interactAction = new InputAction("Interact", InputActionType.Button);
        EnsureDefaultBindings();
    }

    void EnsureDefaultBindings()
    {
        if (interactAction.bindings.Count == 0)
        {
            interactAction.AddBinding("<Mouse>/leftButton");
            interactAction.AddBinding("<Gamepad>/buttonWest");
        }
    }

    void OnEnable()
    {
        EnsureDefaultBindings();
        interactAction.Enable();
    }

    void OnDisable()
    {
        interactAction.Disable();
    }

    void Start()
    {
        // Si olvidaste asignar la cámara en el Inspector, la busca automáticamente
        if (cameraTx == null && Camera.main != null)
        {
            cameraTx = Camera.main.transform;
        }
    }

    void Update()
    {
        if (interactAction.WasPressedThisFrame())
        {
            TryInteract();
        }
    }

    void TryInteract()
    {
        if (cameraTx == null) return;

        Ray ray = new Ray(cameraTx.position, cameraTx.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableMask, QueryTriggerInteraction.Ignore))
        {
            // Busca cualquier script en el objeto golpeado (o sus padres) que implemente IInteractable
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null)
            {
                interactable.Interact();
            }
        }
    }
}