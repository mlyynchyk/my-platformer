using UnityEngine;

// Висить на префабі чекпойнта (SpriteRenderer + BoxCollider2D з Is Trigger).
public class Checkpoint : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private Color inactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField] private Color activeColor = Color.white;

    private bool activated;

    private void Awake()
    {
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
        sprite.color = inactiveColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated) return;

        PlayerRespawn player = other.GetComponentInParent<PlayerRespawn>();
        if (player == null) return;

        player.SetCheckpoint(transform.position);
        activated = true;
        sprite.color = activeColor;
    }
}
