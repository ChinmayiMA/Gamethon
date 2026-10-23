using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// Moves an object along a straight line and wraps it around (traffic, metro trains).
public class LoopingMover : MonoBehaviour
{
    public Vector3 dir = Vector3.forward;
    public float speed = 10f;
    public float limit = 300f;

    void Update()
    {
        transform.position += dir * (speed * Time.deltaTime);
        float t = Vector3.Dot(transform.position, dir);
        if (t > limit) transform.position -= dir * (2f * limit);
    }
}

/// Flickering neon tube or blinking aircraft beacon. Uses a MaterialPropertyBlock (shared material untouched).
public class NeonFlicker : MonoBehaviour
{
    public enum Mode { Flicker, Blink }
    public Mode mode = Mode.Flicker;
    public Color emission = Color.white;
    public float speed = 1f;

    Renderer r; MaterialPropertyBlock mpb; float seed;

    void Awake() { r = GetComponent<Renderer>(); mpb = new MaterialPropertyBlock(); seed = Random.value * 100f; }

    void Update()
    {
        float k;
        if (mode == Mode.Blink)
            k = Mathf.Repeat(Time.time * speed + seed, 2f) < 0.15f ? 1f : 0.05f;
        else
        {
            float n = Mathf.PerlinNoise(Time.time * 6f * speed, seed);
            k = n > 0.3f ? 1f : (Mathf.PerlinNoise(Time.time * 40f, seed) > 0.5f ? 0.7f : 0.05f);
        }
        r.GetPropertyBlock(mpb);
        mpb.SetColor("_EmissionColor", emission * k);
        r.SetPropertyBlock(mpb);
    }
}

/// Slowly scrolls an LED billboard texture.
public class ScrollUV : MonoBehaviour
{
    public Vector2 speed = new Vector2(0f, 0.05f);
    Material m; Vector2 off;

    void Start() { m = GetComponent<Renderer>().material; }

    void Update()
    {
        off += speed * Time.deltaTime;
        m.SetTextureOffset("_BaseMap", off);   // emission map shares the base map's tiling/offset in URP Lit
        m.SetTextureOffset("_MainTex", off);
    }
}

/// Keeps an object (rain) above a target.
public class FollowTarget : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 18, 8);
    void LateUpdate() { if (target) transform.position = target.position + offset; }
}

/// Simple fly camera so you can explore right away. Hold RIGHT MOUSE to look, WASD move, Q/E down/up, Shift = fast.
/// Replace it with your own player controller when you start the game.
public class FreeFlyCamera : MonoBehaviour
{
    public float speed = 12f, fastMultiplier = 4f, lookSpeed = 0.15f;
    float yaw, pitch;

    void Start() { var e = transform.eulerAngles; yaw = e.y; pitch = e.x > 180 ? e.x - 360 : e.x; }

    void Update()
    {
        Vector2 look; Vector3 move; bool fast, look_on;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current; var ms = Mouse.current;
        if (kb == null || ms == null) return;
        look = ms.delta.ReadValue() * lookSpeed;
        look_on = ms.rightButton.isPressed;
        move = new Vector3((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
                           (kb.eKey.isPressed ? 1 : 0) - (kb.qKey.isPressed ? 1 : 0),
                           (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
        fast = kb.leftShiftKey.isPressed;
#else
        look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * (lookSpeed * 10f);
        look_on = Input.GetMouseButton(1);
        move = new Vector3(Input.GetAxisRaw("Horizontal"),
                           (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0),
                           Input.GetAxisRaw("Vertical"));
        fast = Input.GetKey(KeyCode.LeftShift);
#endif
        if (look_on)
        {
            yaw += look.x; pitch -= look.y;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }
        float s = speed * (fast ? fastMultiplier : 1f);
        transform.position += (transform.right * move.x + Vector3.up * move.y + transform.forward * move.z) * (s * Time.deltaTime);
    }
}
