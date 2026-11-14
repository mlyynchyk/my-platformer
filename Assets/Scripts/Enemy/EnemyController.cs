using UnityEngine;
using UnityEngine.Pool;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    [Header("Дані")]
    [SerializeField] private EnemyStatsSO stats;

    [Header("Точки")]
    [SerializeField] private Transform groundCheck;    // під ступнею
    [SerializeField] private Transform edgeCheck;      // перед носком, НИЖЧЕ ступні
    [SerializeField] private Transform wallCheck;      // перед носком, на рівні тулуба
    [SerializeField] private Transform attackPoint;    // звідки йде удар

    [Header("Шари")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask damageableLayers;

    [Header("Звук")]
    [SerializeField] private AudioSource audioSource;

    [Header("Стрільба")]
    [SerializeField] private Projectile projectilePrefab;
    private ObjectPool<Projectile> pool;

    public EnemyStatsSO Stats => stats;
    public Rigidbody2D  Body     { get; private set; }
    public Animator     Animator { get; private set; }
    public StateMachine Machine  { get; private set; }
    public Transform    Target   { get; private set; }

    public PatrolState PatrolState { get; private set; }
    public ChaseState  ChaseState  { get; private set; }
    public HurtState   HurtState   { get; private set; }
    public DeadState   DeadState   { get; private set; }
    public IState      AttackState { get; private set; }

    public int Facing { get; private set; } = 1;

    private void Awake()
    {
        Body     = GetComponent<Rigidbody2D>();
        Animator = GetComponentInChildren<Animator>();
        Machine  = new StateMachine();

        // Префаб не може зберігати посилання на об'єкт сцени —
        // гравця шукаємо в коді
        Target = GameObject.FindGameObjectWithTag("Player").transform;

        GetComponent<Health>().Configure(stats.maxHealth);

        // Вигляд теж приходить з ассета
        GetComponentInChildren<SpriteRenderer>().color = stats.tint;
        ApplyScale();

        PatrolState = new PatrolState(this);
        ChaseState  = new ChaseState(this);
        HurtState   = new HurtState(this);
        DeadState   = new DeadState(this);
        AttackState = stats.ranged
            ? (IState)new RangedAttackState(this)
            : new MeleeAttackState(this);

        if (stats.ranged)
        {
            pool = new ObjectPool<Projectile>(
                createFunc:      () => Instantiate(projectilePrefab),
                actionOnGet:     p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => Destroy(p.gameObject),
                defaultCapacity: 20);
        }
    }

    private void Start()  => Machine.ChangeState(PatrolState);
    private void Update() => Machine.Tick();

    // Відстань міряємо ПО ГОРИЗОНТАЛІ: висоту окремо перевіряє TargetOnSameLevel
    public float DistanceToTarget =>
        Mathf.Abs(Target.position.x - transform.position.x);

    // Рівень порівнюємо по СТУПНЯХ, а не по початках координат
    public bool TargetOnSameLevel =>
        Mathf.Abs(Target.position.y - groundCheck.position.y)
            <= stats.verticalTolerance;

    public void Move(float speed)
    {
        Body.linearVelocity = new Vector2(Facing * speed, Body.linearVelocity.y);
        Animator.SetFloat("Speed", Mathf.Abs(Body.linearVelocity.x));
    }

    public void Stop()
    {
        Body.linearVelocity = new Vector2(0f, Body.linearVelocity.y);
        Animator.SetFloat("Speed", 0f);
    }

    public void Flip()
    {
        Facing = -Facing;
        ApplyScale();
    }

    // Масштаб і дзеркалення задаються разом
    private void ApplyScale() =>
        transform.localScale =
            new Vector3(stats.sizeScale * Facing, stats.sizeScale, 1f);

    private const float FacingDeadZone = 0.1f;

    public void FaceTarget()
    {
        float dx = Target.position.x - transform.position.x;
        if (Mathf.Abs(dx) < FacingDeadZone) return;

        int dir = dx > 0f ? 1 : -1;
        if (dir != Facing) Flip();
    }

    private const float ProbeRadius = 0.1f;

    public bool IsGrounded =>
        Physics2D.OverlapCircle(groundCheck.position, ProbeRadius, groundLayer);

    // Під носком НЕМАЄ землі — попереду край платформи
    public bool AtEdge() =>
        !Physics2D.OverlapCircle(edgeCheck.position, ProbeRadius, groundLayer);

    // Перед носком Є перешкода — попереду стіна
    public bool AtWall() =>
        Physics2D.OverlapCircle(wallCheck.position, ProbeRadius, groundLayer);

    public void PlayTelegraph()
    {
        if (stats.telegraphSfx != null)
            audioSource.PlayOneShot(stats.telegraphSfx);
    }

    public void DealDamage()
    {
        Collider2D hit = Physics2D.OverlapCircle(
            attackPoint.position, stats.attackRadius, damageableLayers);

        if (hit != null && hit.TryGetComponent(out IDamageable target))
            target.TakeDamage(stats.damage, transform.position);
    }

    public void Shoot()
    {
        Projectile p = pool.Get();

        // Стан скидаємо ЯВНО: перевикористаний снаряд пам'ятає старий
        p.transform.position = attackPoint.position;
        p.OnFinished = Release;
        p.Launch(new Vector2(Facing, 0f), stats.damage);
    }

    private void Release(Projectile p)
    {
        // При виході з Play Unity знищує снаряди раніше за пул
        if (p == null) return;

        p.OnFinished = null;
        pool.Release(p);
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
            Gizmos.DrawWireSphere(attackPoint.position,
                stats != null ? stats.attackRadius : 0.5f);
        if (groundCheck != null) Gizmos.DrawWireSphere(groundCheck.position, ProbeRadius);
        if (edgeCheck != null)   Gizmos.DrawWireSphere(edgeCheck.position, ProbeRadius);
        if (wallCheck != null)   Gizmos.DrawWireSphere(wallCheck.position, ProbeRadius);
    }
}
