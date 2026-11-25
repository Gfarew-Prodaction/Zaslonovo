using UnityEngine;
using UnityEngine.InputSystem;

namespace Zaslonovo
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CampFirstPersonController : MonoBehaviour
    {
        public Camera viewCamera;
        [Min(.1f)] public float walkSpeed = 4.2f;
        [Min(.1f)] public float runSpeed = 7.2f;
        [Min(.01f)] public float mouseSensitivity = .085f;
        public float jumpHeight = .9f;
        public float gravity = -22f;
        public Vector3 spawnPosition;
        public float spawnYaw;
        public bool captureCursorOnStart = true;
        public bool showControls = true;
        CharacterController controller;
        float verticalSpeed;
        float pitch;
        bool captured;
        GUIStyle helpStyle;
        public bool Captured => captured;
        public bool Grounded => controller != null && controller.isGrounded;
        public float Pitch => pitch;
        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        }
        void Start()
        {
            if (captureCursorOnStart) SetCapture(true);
        }
        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) SetCapture(false);
            if (!captured && mouse != null && mouse.leftButton.wasPressedThisFrame && Application.isFocused) SetCapture(true);
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Respawn();
            Vector2 movement = Vector2.zero;
            Vector2 look = Vector2.zero;
            bool running = false, jump = false;
            if (captured && Application.isFocused)
            {
                if (keyboard != null)
                {
                    movement = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                    running = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                    jump = keyboard.spaceKey.wasPressedThisFrame;
                }
                if (mouse != null) look = mouse.delta.ReadValue();
            }
            SimulateFrame(movement, look, running, jump, Time.deltaTime);
            if (transform.position.y < -15) Respawn();
        }
        // Kept separate from device polling so movement and collision can be checked deterministically.
        public void SimulateFrame(Vector2 movement, Vector2 mouseDelta, bool running, bool jump, float deltaTime)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (viewCamera == null || deltaTime <= 0 || !controller.enabled) return;
            transform.Rotate(0, mouseDelta.x * mouseSensitivity, 0, Space.Self);
            pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseSensitivity, -85, 85);
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2f;
            if (jump && controller.isGrounded) verticalSpeed = Mathf.Sqrt(jumpHeight * -2 * gravity);
            verticalSpeed += gravity * deltaTime;
            Vector2 input = Vector2.ClampMagnitude(movement, 1);
            Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * (running ? runSpeed : walkSpeed);
            velocity.y = verticalSpeed;
            controller.Move(velocity * deltaTime);
        }
        public void SetCapture(bool value)
        {
            captured = value;
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !value;
        }
        public void Respawn()
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, Quaternion.Euler(0, spawnYaw, 0));
            verticalSpeed = 0; pitch = 0;
            if (viewCamera != null) viewCamera.transform.localRotation = Quaternion.identity;
            controller.enabled = true;
        }
        void OnApplicationFocus(bool focused) { if (!focused) SetCapture(false); }
        void OnDisable() { SetCapture(false); }
        void OnGUI()
        {
            if (!showControls) return;
            if (helpStyle == null) helpStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(.95f,.96f,.90f) } };
            GUI.color = new Color(.02f,.035f,.025f,.68f);
            GUI.DrawTexture(new Rect(18, Screen.height - 58, 630, 39), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(30, Screen.height - 50, 612, 25), captured ? "WASD — ходьба   Shift — бег   Мышь — обзор   Space — прыжок   Esc — курсор" : "Нажмите мышью для прогулки • R — вернуться к началу", helpStyle);
            if (captured)
            {
                GUI.color = new Color(1,1,1,.78f);
                GUI.DrawTexture(new Rect(Screen.width/2f-3,Screen.height/2f-.7f,6,1.4f),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(Screen.width/2f-.7f,Screen.height/2f-3,1.4f,6),Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }
    }
}
