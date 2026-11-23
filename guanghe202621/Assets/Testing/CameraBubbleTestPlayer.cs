using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>仅用于无资源测试场景：WASD/方向键移动方块，验证气泡跟随和触发器。</summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class CameraBubbleTestPlayer : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 3f;
    private Rigidbody2D body;
    private Vector2 input;
    private Vector2 spawnPosition;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        // 测试玩家由 MovePosition 驱动，必须使用可移动的刚体。
        // 同时在这里修正旧场景的 Static 设置，避免与静态触发区接触时收不到事件。
        body.bodyType = RigidbodyType2D.Kinematic;
        body.simulated = true;
        body.gravityScale = 0f;
        spawnPosition = body.position;
    }

    private void Update()
    {
        input = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x--;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x++;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y--;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y++;
#else
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        input = Vector2.ClampMagnitude(input, 1f);
    }

    private void FixedUpdate()
    {
        Vector2 position = body.position + input * moveSpeed * Time.fixedDeltaTime;
        position.x = Mathf.Clamp(position.x, -12f, 12f);
        position.y = Mathf.Clamp(position.y, -1f, 2f);
        body.MovePosition(position);
    }

    public void ResetPosition()
    {
        input = Vector2.zero;
        body.position = spawnPosition;
        transform.position = new Vector3(spawnPosition.x, spawnPosition.y, transform.position.z);
    }
}
