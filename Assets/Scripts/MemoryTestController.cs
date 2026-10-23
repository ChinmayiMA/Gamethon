using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EchoDelete
{
    // Attached only to MemoryTest. All generated objects belong to that scene.
    public sealed class MemoryTestController : MonoBehaviour
    {
        private MemoryManager memory;
        private MemoryTerminal terminal;
        private Camera view;
        private CharacterController player;
        private float yaw, pitch, verticalSpeed;
        private readonly Material[] materials = new Material[4];
        private const float Reach = 3f;

        private void Start()
        {
            memory = gameObject.AddComponent<MemoryManager>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Color[] colors = { new Color(.08f,.1f,.16f), Color.cyan, Color.magenta, Color.yellow };
            for (int i = 0; i < materials.Length; i++)
                materials[i] = new Material(shader) { color = colors[i] };
            Box("Floor", new Vector3(0,-.25f,3), new Vector3(14,.5f,16), 0);
            terminal = Box("Delete Terminal", new Vector3(0,1,2), new Vector3(1,2,.7f), 1)
                .AddComponent<MemoryTerminal>();
            for (int i = 0; i < 3; i++)
            {
                var target = Box("Memory fragment " + (i + 1), new Vector3(-3 + i * 3,1,6), Vector3.one * 2, i + 1);
                gameObject.AddComponent<MemoryThresholdObject>().Initialize(memory, target, 75 - i * 25);
            }
            var light = new GameObject("Test Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(50,-30,0);
            player = new GameObject("Player").AddComponent<CharacterController>();
            player.height = 1.8f;
            player.center = Vector3.up * .9f;
            player.transform.position = new Vector3(0,.05f,-1);
            view = new GameObject("Player Camera", typeof(AudioListener)).AddComponent<Camera>();
            view.tag = "MainCamera";
            view.transform.SetParent(player.transform, false);
            view.transform.localPosition = Vector3.up * 1.6f;
            view.backgroundColor = new Color(.02f,.025f,.05f);
            view.clearFlags = CameraClearFlags.SolidColor;
        }

        private GameObject Box(string label, Vector3 position, Vector3 scale, int material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = label;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = materials[material];
            return box;
        }

        private bool CanInteract() => view != null && Physics.Raycast(view.transform.position,
            view.transform.forward, out var hit, Reach) && hit.collider.GetComponent<MemoryTerminal>() == terminal;

        private void Update()
        {
            if (player == null) return;
            Vector2 move = Vector2.zero, look = Vector2.zero;
            bool interact = false, looking = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null)
            {
                move = new Vector2((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
                    (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
                interact = kb.eKey.wasPressedThisFrame;
            }
            if (mouse != null) { looking = mouse.rightButton.isPressed; look = mouse.delta.ReadValue() * .12f; }
#else
            move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            interact = Input.GetKeyDown(KeyCode.E);
            looking = Input.GetMouseButton(1);
            look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2f;
#endif
            if (looking) { yaw += look.x; pitch = Mathf.Clamp(pitch - look.y,-80,80); }
            player.transform.rotation = Quaternion.Euler(0,yaw,0);
            view.transform.localRotation = Quaternion.Euler(pitch,0,0);
            if (player.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            Vector3 direction = player.transform.TransformDirection(new Vector3(move.x,0,move.y).normalized);
            player.Move((direction * 4 + Vector3.up * verticalSpeed) * Time.deltaTime);
            if (interact && CanInteract()) terminal.Interact(memory);
        }

        private void OnGUI()
        {
            if (memory == null) return;
            GUI.Box(new Rect(16,16,460,135), "ECHO//DELETE — MEMORY TEST");
            GUI.Label(new Rect(30,44,430,100), "RAM: " + memory.RemainingRam + " / 100\nWASD: move | Hold right mouse: look | E: delete 25 RAM\nFragments disappear at 75 / 50 / 25 RAM.\n" + terminal.Status);
            GUI.Label(new Rect(Screen.width/2f-5,Screen.height/2f-12,20,24), "+");
            if (CanInteract()) GUI.Box(new Rect(Screen.width/2f-150,Screen.height/2f+30,300,30), "[E] Delete memory — 25 RAM");
        }

        private void OnDestroy()
        {
            foreach (var material in materials) if (material != null) Destroy(material);
        }
    }
}
