using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Рух")]
    [SerializeField, Range(1f, 20f)] private float maxSpeed = 8f;
    [SerializeField, Range(5f, 200f)] private float acceleration = 50f;
    [SerializeField, Range(5f, 200f)] private float deceleration = 50f;

    [Header("Стрибок")]
    [SerializeField, Range(0.5f, 8f)] private float jumpHeight = 2.5f;
    [SerializeField, Range(0.5f, 8f)] private float baseGravity = 3f;
    [SerializeField, Range(0f, 0.4f)] private float coyoteTimeDuration = 0.12f;
    [SerializeField, Range(0f, 0.4f)] private float jumpBufferDuration = 0.12f;
    [SerializeField, Range(1f, 6f)] private float fallMultiplier = 2.2f;
    [SerializeField, Range(1f, 8f)] private float lowJumpMultiplier = 3.5f;

    [Header("Перевірка землі")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField, Range(0.05f, 0.5f)] private float groundCheckRadius = 0.2f;

    [Header("Вигляд")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Зворотний звʼязок")]
    [SerializeField, Range(1f, 20f)] private float hardLandingSpeed = 9f;
    [SerializeField] private CinemachineImpulseSource impulseSource; // тряска через Cinemachine

    [Header("Взаємодія")]
    [SerializeField] private Transform interactPoint;
    [SerializeField, Range(0.2f, 1.5f)] private float interactRadius = 0.6f;
    [SerializeField] private LayerMask interactableLayer;

    [Header("Звук")]
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip[] footsteps;   // щонайменше ТРИ кліпи
    [SerializeField, Range(0f, 0.3f)] private float pitchVariance = 0.12f;
    [SerializeField, Range(0.1f, 0.8f)] private float stepInterval = 0.32f;

    private Rigidbody2D body;
    private PlayerControls controls;
    private float moveInput;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool isGrounded;

    private bool wasGrounded;
    private float fallSpeed; // найбільша швидкість падіння перед дотиком
    private float stepTimer;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        controls = new PlayerControls();

        // Префаб не може зберігати посилання на обʼєкт сцени — шукаємо самі[cite: 8].
        if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void OnEnable() => controls.Player.Enable();
    private void OnDisable() => controls.Player.Disable();
    private void OnDestroy() => controls.Dispose();

    private void Update()
    {
        // Гра на паузі: ввід не читаємо взагалі. Інакше гравець
        // стрибає й натискає E крізь відкрите меню
        if (PauseController.IsPaused) return;

        // Перевірка земної поверхні
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // Таймер Coyote Time (поповнюємо тільки коли стоїмо або падаємо)
        bool canRefill = isGrounded && body.linearVelocity.y <= 0.01f;
        coyoteTimeCounter = canRefill ? coyoteTimeDuration : coyoteTimeCounter - Time.deltaTime;

        // Буферизація стрибка (клавіша Space)
        bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (jumpPressed)
        {
            jumpBufferCounter = jumpBufferDuration;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        // Виконання стрибка
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            Jump();
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }

        // Взаємодія (клавіша E)
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            TryInteract();

        // Перевірка приземлення та обчислення швидкості падіння
        CheckLanding();

        // Обробка звуків кроків
        UpdateFootsteps();

        // Зміна гравітації для чутливого падіння та короткого стрибка
        bool jumpHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        if (body.linearVelocity.y < 0f)
        {
            body.gravityScale = baseGravity * fallMultiplier;
        }
        else if (body.linearVelocity.y > 0f && !jumpHeld)
        {
            body.gravityScale = baseGravity * lowJumpMultiplier;
        }
        else
        {
            body.gravityScale = baseGravity;
        }

        // Надійне пряме зчитування клавіш руху (A/D або стрілочки)
        moveInput = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveInput += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveInput -= 1f;
        }

        UpdateVisuals();
    }

    private void FixedUpdate()
    {
        // Плавний горизонтальний рух
        float targetSpeed = moveInput * maxSpeed;
        float speedRate = Mathf.Abs(moveInput) > 0.01f ? acceleration : deceleration;
        float newVelocityX = Mathf.MoveTowards(body.linearVelocity.x, targetSpeed, speedRate * Time.fixedDeltaTime);

        body.linearVelocity = new Vector2(newVelocityX, body.linearVelocity.y);

        if (Mathf.Abs(moveInput) > 0.01f)
            Debug.Log($"input={moveInput}  velX={body.linearVelocity.x:F2}  pos={body.position.x:F2}");
    }

    private void Jump()
    {
        body.gravityScale = baseGravity;
        float gravity = Mathf.Abs(Physics2D.gravity.y * baseGravity);
        float jumpForce = Mathf.Sqrt(2f * gravity * jumpHeight);
        body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
    }

    private void CheckLanding()
    {
        // Поки летимо вниз — памʼятаємо, наскільки швидко
        if (!isGrounded)
            fallSpeed = Mathf.Max(fallSpeed, -body.linearVelocity.y);

        if (isGrounded && !wasGrounded)  // ← саме ПЕРЕХІД, а не стан[cite: 8]
            OnLanded(fallSpeed);

        if (isGrounded) fallSpeed = 0f;
        wasGrounded = isGrounded;
    }

    private void TryInteract()
    {
        if (interactPoint == null) return;

        Collider2D hit = Physics2D.OverlapCircle(
            interactPoint.position, interactRadius, interactableLayer);

        if (hit != null && hit.TryGetComponent(out IInteractable target))
            target.Interact();
    }

    private void OnLanded(float speed)
    {
        // ТІЛЬКИ жорстке приземлення. Легкий зскок зі сходинки ефекту не дає[cite: 8]
        if (speed < hardLandingSpeed) return;

        if (impulseSource != null)
            impulseSource.GenerateImpulseWithForce(0.3f);
    }

    private void UpdateFootsteps()
    {
        bool running = isGrounded && Mathf.Abs(body.linearVelocity.x) > 0.5f;

        // Не біжимо або у повітрі — миттєво зупиняємо звук кроків і скидаємо таймер
        if (!running)
        {
            if (source.isPlaying)
            {
                source.Stop();
            }
            stepTimer = 0f;
            return;
        }

        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            PlayFootstep();
            stepTimer = stepInterval;
        }
    }

    private void PlayFootstep()
    {
        if (footsteps.Length == 0 || source == null) return;

        AudioClip clip = footsteps[Random.Range(0, footsteps.Length)];
        source.clip = clip;
        source.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
        source.Play();
    }

    private void UpdateVisuals()
    {
        animator.SetFloat(SpeedHash, Mathf.Abs(body.linearVelocity.x));

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            spriteRenderer.flipX = moveInput < 0f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Application.isPlaying && isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}