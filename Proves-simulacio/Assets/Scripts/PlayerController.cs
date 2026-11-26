using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(CharacterController))]
public class BasicFPCC : MonoBehaviour
{
    [Header("Layer Mask")]
    [Tooltip("Layer Mask for sphere/raycasts. Assign the Player object to a Layer, then Ignore that layer here.")]
    public LayerMask castingMask = ~0;

    // - Components -
    private CharacterController controller;
    private Transform playerTx;

    [Header("Main Camera")]
    [Tooltip("Drag the FPC Camera here")]
    public Transform cameraTx;

    [Header("Optional Player Graphic")]
    [Tooltip("optional capsule to visualize player in scene view")]
    public Transform playerGFX;

    [Header("New Input System Actions")]
    [Tooltip("Disable if sending inputs from an external script")]
    public bool useLocalInputs = true;
    [Space(5)]
    public InputAction lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
    public InputAction moveAction = new InputAction("Move", InputActionType.Value);
    public InputAction runAction = new InputAction("Run", InputActionType.Button, "<Keyboard>/leftShift");
    public InputAction crouchAction = new InputAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
    public InputAction toggleCursorAction = new InputAction("ToggleCursor", InputActionType.Button, "<Keyboard>/backquote");

    // Input Variables that can be assigned externally
    [HideInInspector] public float inputLookX = 0;
    [HideInInspector] public float inputLookY = 0;
    [HideInInspector] public float inputMoveX = 0;
    [HideInInspector] public float inputMoveY = 0;
    [HideInInspector] public bool inputKeyRun = false;
    [HideInInspector] public bool inputKeyCrouch = false;
    [HideInInspector] public bool inputKeyDownCursor = false;

    [Header("Look Settings")]
    public float mouseSensitivityX = 0.2f;
    public float mouseSensitivityY = 0.2f;
    [Tooltip("larger values for less filtering, more responsiveness")]
    public float mouseSnappiness = 20f;
    public bool invertLookY = false;
    public float clampLookY = 90f;

    [Header("Move Settings")]
    public float crouchSpeed = 3f;
    public float walkSpeed = 7f;
    public float runSpeed = 12f;
    public float gravity = -9.81f;

    [Header("Grounded Settings")]
    public float groundCheckY = 0.33f;
    public float ceilingCheckY = 1.83f;
    [Space(5)]
    public float sphereCastRadius = 0.25f;
    public float sphereCastDistance = 0.75f;
    [Space(5)]
    public float raycastLength = 0.75f;
    public Vector3 rayOriginOffset1 = new Vector3(-0.2f, 0f, 0.16f);
    public Vector3 rayOriginOffset2 = new Vector3(0.2f, 0f, -0.16f);

    [Header("Debug Gizmos")]
    public bool showGizmos = false;

    // - private reference variables -
    private float defaultHeight = 0;
    private float cameraStartY = 0;

    [Header("- reference variables -")]
    public float xRotation = 0f;
    private float lastSpeed = 0;
    private Vector3 fauxGravity = Vector3.zero;
    private float accMouseX = 0;
    private float accMouseY = 0;
    private Vector3 lastPos = Vector3.zero;
    [Space(5)]
    public bool isGrounded = false;
    public float groundSlopeAngle = 0f;
    public Vector3 groundSlopeDir = Vector3.zero;
    private float groundOffsetY = 0;
    public bool isSlipping = false;
    [Space(5)]
    public bool isCeiling = false;
    private float ceilingOffsetY = 0;
    [Space(5)]
    public bool cursorActive = false;

    void Reset()
    {
        lookAction = new InputAction("Look", InputActionType.Value);
        moveAction = new InputAction("Move", InputActionType.Value);
        runAction = new InputAction("Run", InputActionType.Button);
        crouchAction = new InputAction("Crouch", InputActionType.Button);
        toggleCursorAction = new InputAction("ToggleCursor", InputActionType.Button);

        EnsureDefaultBindings();
    }

    void EnsureDefaultBindings()
    {
        if (lookAction.bindings.Count == 0)
        {
            lookAction.AddBinding("<Mouse>/delta");
            lookAction.AddBinding("<Gamepad>/rightStick");
        }

        if (moveAction.bindings.Count == 0)
        {
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddBinding("<Gamepad>/leftStick");
        }

        if (runAction.bindings.Count == 0)
        {
            runAction.AddBinding("<Keyboard>/leftShift");
            runAction.AddBinding("<Gamepad>/leftStickPress");
        }

        if (crouchAction.bindings.Count == 0)
        {
            crouchAction.AddBinding("<Keyboard>/leftCtrl");
            crouchAction.AddBinding("<Gamepad>/buttonEast");
        }

        if (toggleCursorAction.bindings.Count == 0)
        {
            toggleCursorAction.AddBinding("<Keyboard>/backquote");
        }
    }

    void OnEnable()
    {
        EnsureDefaultBindings();

        lookAction.Enable();
        moveAction.Enable();
        runAction.Enable();
        crouchAction.Enable();
        toggleCursorAction.Enable();
    }

    void OnDisable()
    {
        lookAction.Disable();
        moveAction.Disable();
        runAction.Disable();
        crouchAction.Disable();
        toggleCursorAction.Disable();
    }

    void Start()
    {
        Initialize();
    }

    void Update()
    {
        ProcessInputs();
        ProcessLook();
        ProcessMovement();
    }

    void Initialize()
    {
        if (!cameraTx) { Debug.LogError("* " + gameObject.name + ": BasicFPCC has NO CAMERA ASSIGNED in the Inspector *"); }

        controller = GetComponent<CharacterController>();

        // Si castingMask está en Nothing (0), detectar todo excepto la propia capa del jugador
        if (castingMask.value == 0)
        {
            castingMask = ~(1 << gameObject.layer);
        }

        playerTx = transform;
        defaultHeight = controller.height;
        lastSpeed = 0;
        fauxGravity = Vector3.up * gravity;
        lastPos = playerTx.position;
        if (cameraTx) cameraStartY = cameraTx.localPosition.y;
        groundOffsetY = groundCheckY;
        ceilingOffsetY = ceilingCheckY;

        RefreshCursor();
    }

    void ProcessInputs()
    {
        if (useLocalInputs)
        {
            Vector2 lookVector = lookAction.ReadValue<Vector2>();
            inputLookX = lookVector.x;
            inputLookY = lookVector.y;

            Vector2 moveVector = moveAction.ReadValue<Vector2>();
            inputMoveX = moveVector.x;
            inputMoveY = moveVector.y;

            inputKeyRun = runAction.IsPressed();
            inputKeyCrouch = crouchAction.IsPressed();
            inputKeyDownCursor = toggleCursorAction.WasPressedThisFrame();
        }

        if (inputKeyDownCursor)
        {
            ToggleLockCursor();
        }
    }

    void ProcessLook()
    {
        if (!cameraTx) return;

        accMouseX = Mathf.Lerp(accMouseX, inputLookX, mouseSnappiness * Time.deltaTime);
        accMouseY = Mathf.Lerp(accMouseY, inputLookY, mouseSnappiness * Time.deltaTime);

        float mouseX = accMouseX * mouseSensitivityX * 100f * Time.deltaTime;
        float mouseY = accMouseY * mouseSensitivityY * 100f * Time.deltaTime;

        xRotation += (invertLookY ? mouseY : -mouseY);
        xRotation = Mathf.Clamp(xRotation, -clampLookY, clampLookY);

        cameraTx.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerTx.Rotate(Vector3.up * mouseX);
    }

    void ProcessMovement()
    {
        float vScale = 1f;
        float h = defaultHeight;
        float nextSpeed = walkSpeed;
        Vector3 calc;

        float currSpeed = (playerTx.position - lastPos).magnitude / Time.deltaTime;
        currSpeed = Mathf.Abs(currSpeed);
        lastPos = playerTx.position;

        GroundCheck();

        isSlipping = (groundSlopeAngle > controller.slopeLimit);

        CeilingCheck();

        // - Run & Crouch -
        if (isGrounded && !isCeiling && inputKeyRun)
        {
            nextSpeed = runSpeed;
        }

        if (inputKeyCrouch)
        {
            vScale = 0.5f;
            h = 0.5f * defaultHeight;
            nextSpeed = crouchSpeed;
        }

        // - Player Move Input -
        Vector3 move = (playerTx.right * inputMoveX) + (playerTx.forward * inputMoveY);
        if (move.magnitude > 1f)
        {
            move = move.normalized;
        }

        // - Height (Crouch / Stand up smoothly) -
        float lastHeight = controller.height;
        float nextHeight = Mathf.Lerp(controller.height, h, 5f * Time.deltaTime);

        if (nextHeight < lastHeight || !isCeiling)
        {
            controller.height = Mathf.Lerp(controller.height, h, 5f * Time.deltaTime);

            calc = playerTx.position;
            calc.y += (controller.height - lastHeight) / 2f;
            playerTx.position = calc;

            if (cameraTx)
            {
                calc = cameraTx.localPosition;
                calc.y = (controller.height / defaultHeight) + cameraStartY - (defaultHeight * 0.5f);
                cameraTx.localPosition = calc;
            }

            float heightFactor = (defaultHeight - controller.height) * 0.5f;
            groundOffsetY = heightFactor + groundCheckY;
            ceilingOffsetY = heightFactor + controller.height - (defaultHeight - ceilingCheckY);

            if (playerGFX)
            {
                calc = playerGFX.localScale;
                calc.y = Mathf.Lerp(calc.y, vScale, 5f * Time.deltaTime);
                playerGFX.localScale = calc;
            }
        }

        // - Slipping & Gravity -
        float speed;

        if (isGrounded)
        {
            if (isSlipping)
            {
                Vector3 slopeRight = Quaternion.LookRotation(Vector3.right) * groundSlopeDir;
                float dot = Vector3.Dot(slopeRight, playerTx.right);
                move = slopeRight * (dot > 0 ? inputMoveX : -inputMoveX);

                nextSpeed = Mathf.Lerp(currSpeed, runSpeed, 5f * Time.deltaTime);

                float mag = fauxGravity.magnitude;
                calc = Vector3.Slerp(fauxGravity, groundSlopeDir * runSpeed, 4f * Time.deltaTime);
                fauxGravity = calc.normalized * mag;
            }
            else
            {
                fauxGravity.x = 0;
                fauxGravity.z = 0;

                if (fauxGravity.y < 0)
                {
                    fauxGravity.y = Mathf.Lerp(fauxGravity.y, -1f, 4f * Time.deltaTime);
                }
            }

            float lerpFactor = (lastSpeed > nextSpeed ? 4f : 2f);
            speed = Mathf.Lerp(lastSpeed, nextSpeed, lerpFactor * Time.deltaTime);
        }
        else
        {
            speed = Mathf.Lerp(lastSpeed, nextSpeed, 0.125f * Time.deltaTime);
        }

        if (isCeiling)
        {
            speed = crouchSpeed;

            if (fauxGravity.y > 0)
            {
                fauxGravity.y = -1f;
            }
        }

        lastSpeed = speed;
        fauxGravity.y += gravity * Time.deltaTime;

        calc = move * speed * Time.deltaTime;
        calc += fauxGravity * Time.deltaTime;

        controller.Move(calc);

#if UNITY_EDITOR
        if (showGizmos)
        {
            calc = playerTx.position;
            calc.y += groundOffsetY;
            Debug.DrawRay(calc, groundSlopeDir.normalized * 5f, Color.blue);
            Debug.DrawRay(calc, fauxGravity, Color.green);
        }
#endif
    }

    public void SetLockCursor(bool doLock)
    {
        cursorActive = doLock;
        RefreshCursor();
    }

    void ToggleLockCursor()
    {
        cursorActive = !cursorActive;
        RefreshCursor();
    }

    void RefreshCursor()
    {
        if (!cursorActive && Cursor.lockState != CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.Locked; }
        if (cursorActive && Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; }
    }

    void CeilingCheck()
    {
        float topY = controller.bounds.max.y - sphereCastRadius + controller.skinWidth;
        Vector3 origin = new Vector3(playerTx.position.x, topY, playerTx.position.z);

        // Ignoramos los colliders que pertenezcan al propio jugador
        Collider[] hits = Physics.OverlapSphere(origin, sphereCastRadius, castingMask, QueryTriggerInteraction.Ignore);
        isCeiling = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].transform.root != playerTx.root)
            {
                isCeiling = true;
                break;
            }
        }
    }

    void GroundCheck()
    {
        // Calculamos el origen desde la base real del CharacterController (funciona sin importar el Center)
        float bottomY = controller.bounds.min.y + sphereCastRadius + controller.skinWidth;
        Vector3 origin = new Vector3(playerTx.position.x, bottomY, playerTx.position.z);
        RaycastHit hit;

        if (Physics.SphereCast(origin, sphereCastRadius, Vector3.down, out hit, sphereCastDistance, castingMask, QueryTriggerInteraction.Ignore))
        {
            groundSlopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            Vector3 temp = Vector3.Cross(hit.normal, Vector3.down);
            groundSlopeDir = Vector3.Cross(temp, hit.normal);
            isGrounded = true;
        }
        else
        {
            // Respaldo nativo del CharacterController
            isGrounded = controller.isGrounded;
            groundSlopeAngle = 0f;
            groundSlopeDir = Vector3.zero;
        }

        RaycastHit slopeHit1;
        RaycastHit slopeHit2;

        if (Physics.Raycast(origin + rayOriginOffset1, Vector3.down, out slopeHit1, raycastLength, castingMask, QueryTriggerInteraction.Ignore))
        {
#if UNITY_EDITOR
            if (showGizmos) { Debug.DrawLine(origin + rayOriginOffset1, slopeHit1.point, Color.red); }
#endif
            float angleOne = Vector3.Angle(slopeHit1.normal, Vector3.up);

            if (Physics.Raycast(origin + rayOriginOffset2, Vector3.down, out slopeHit2, raycastLength, castingMask, QueryTriggerInteraction.Ignore))
            {
#if UNITY_EDITOR
                if (showGizmos) { Debug.DrawLine(origin + rayOriginOffset2, slopeHit2.point, Color.red); }
#endif
                float angleTwo = Vector3.Angle(slopeHit2.normal, Vector3.up);
                float[] tempArray = new float[] { groundSlopeAngle, angleOne, angleTwo };
                System.Array.Sort(tempArray);
                groundSlopeAngle = tempArray[1];
            }
            else
            {
                float average = (groundSlopeAngle + angleOne) / 2;
                groundSlopeAngle = average;
            }
        }
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        if (body == null || body.isKinematic)
        {
            return;
        }

        if (hit.moveDirection.y < -0.3f)
        {
            return;
        }

        body.linearVelocity = hit.moveDirection * lastSpeed;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (showGizmos)
        {
            if (!Application.isPlaying)
            {
                groundOffsetY = groundCheckY;
                ceilingOffsetY = ceilingCheckY;
            }

            Vector3 startPoint = new Vector3(transform.position.x, transform.position.y + groundOffsetY, transform.position.z);
            Vector3 endPoint = startPoint + new Vector3(0, -sphereCastDistance, 0);
            Vector3 ceilingPoint = new Vector3(transform.position.x, transform.position.y + ceilingOffsetY, transform.position.z);

            Gizmos.color = (isGrounded ? Color.green : Color.white);
            Gizmos.DrawWireSphere(startPoint, sphereCastRadius);

            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(endPoint, sphereCastRadius);

            Gizmos.DrawLine(startPoint, endPoint);

            Gizmos.color = (isCeiling ? Color.red : Color.white);
            Gizmos.DrawWireSphere(ceilingPoint, sphereCastRadius);
        }
    }
#endif
}

public class BasicFPCC_Setup : MonoBehaviour
{
#if UNITY_EDITOR
    private static int playerLayer = 2;

    [MenuItem("GameObject/3D Object/BasicFPCC", false, 0)]
    public static void CreateBasicFPCC()
    {
        GameObject go = new GameObject("Player");

        CharacterController controller = go.AddComponent<CharacterController>();
        controller.center = new Vector3(0, 1, 0);

        BasicFPCC basicFPCC = go.AddComponent<BasicFPCC>();

        go.layer = playerLayer;
        basicFPCC.castingMask = ~(1 << playerLayer);
        Debug.LogWarning(
            "Assign the BasicFPCC Player object to its own Layer, then assign the Layer Mask to ignore the BasicFPCC Player object Layer. Currently using layer "
            + playerLayer.ToString() + ": " + LayerMask.LayerToName(playerLayer)
        );

        GameObject mainCamObject = GameObject.Find("Main Camera");
        if (mainCamObject)
        {
            mainCamObject.transform.parent = go.transform;
            mainCamObject.transform.localPosition = new Vector3(0, 1.7f, 0);
            mainCamObject.transform.localRotation = Quaternion.identity;

            basicFPCC.cameraTx = mainCamObject.transform;
        }
        else
        {
            GameObject camGo = new GameObject("BasicFPCC Camera");
            camGo.AddComponent<Camera>();

            camGo.transform.parent = go.transform;
            camGo.transform.localPosition = new Vector3(0, 1.7f, 0);
            camGo.transform.localRotation = Quaternion.identity;

            basicFPCC.cameraTx = camGo.transform;
        }

        GameObject gfx = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Collider cc = gfx.GetComponent<Collider>();
        DestroyImmediate(cc);
        gfx.transform.parent = go.transform;
        gfx.transform.localPosition = new Vector3(0, 1, 0);
        gfx.name = "GFX";
        gfx.layer = playerLayer;
        basicFPCC.playerGFX = gfx.transform;
    }
#endif
}   