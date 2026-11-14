using UnityEngine;

// Висить на обʼєкті Hazards (Tilemap з Is Trigger). Дотик гравця = повернення на чекпойнт.
public class Hazard : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerRespawn player = other.GetComponentInParent<PlayerRespawn>();
        if (player != null) player.Respawn();
    }
}
