using UnityEngine;

public class EnemyHealth : Health
{
    [SerializeField] private EnemyController controller;
    [SerializeField] private Collider2D hitbox;

    protected override void OnDamaged()
    {
        controller.Machine.ChangeState(controller.HurtState);
    }

    protected override void Die()
    {
        controller.Machine.ChangeState(controller.DeadState);

        // Обидва рядки обов'язкові: інакше «труп» продовжує
        // отримувати урон і бити гравця
        hitbox.enabled = false;
        controller.enabled = false;

        // Без фізики труп не провалюється крізь землю, коли колайдер вимкнено
        GetComponent<Rigidbody2D>().simulated = false;

        Destroy(gameObject, 1.2f);   // встигає програтися анімація смерті
    }
}
