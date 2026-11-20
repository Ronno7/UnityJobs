using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public sealed class PlayerTarget : MonoBehaviour
{
    [SerializeField] private Camera sceneCamera;
    [SerializeField, Min(0.1f)] private float moveSpeed = 8f;

    private InputAction move;

    private void Awake()
    {
        // Use the project's Move bindings (WASD, arrow keys and gamepad stick).
        move = InputSystem.actions.FindAction("Player/Move", throwIfNotFound: true);
    }

    private void Update()
    {
        Vector3 position = transform.position;
        Vector2 movement = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
        position += (Vector3)(movement * (moveSpeed * Time.deltaTime));

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            Vector2 pointer = mouse.position.ReadValue();
            float depth = position.z - sceneCamera.transform.position.z;
            position = sceneCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, depth));
        }

        Vector3 viewport = sceneCamera.WorldToViewportPoint(position);
        viewport.x = Mathf.Clamp(viewport.x, 0.02f, 0.98f);
        viewport.y = Mathf.Clamp(viewport.y, 0.03f, 0.97f);
        transform.position = sceneCamera.ViewportToWorldPoint(viewport);
    }
}
