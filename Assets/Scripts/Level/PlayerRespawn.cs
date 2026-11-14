using UnityEngine;

// Висить на гравці. Памʼятає останній чекпойнт і повертає гравця на нього.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerRespawn : MonoBehaviour
{
    [SerializeField] private float fallLimitY = -15f; // нижче цієї висоти гравець "випав зі світу"

    private Rigidbody2D body;
    private Vector3 respawnPoint;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        respawnPoint = transform.position; // стартова точка рівня
    }

    private void Update()
    {
        if (transform.position.y < fallLimitY) Respawn();
    }

    public void SetCheckpoint(Vector3 point)
    {
        respawnPoint = point;
    }

    public void Respawn()
    {
        transform.position = respawnPoint;
        body.linearVelocity = Vector2.zero;
    }
}
