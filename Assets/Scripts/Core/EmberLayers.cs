using UnityEngine;

// Physics layers used by the game, and the masks built from them. Every raycast and overlap query uses one of these,
// so adding a layer never silently changes what the player stands on or what blocks the camera.
public static class EmberLayers
{
    public const string Player = "Player";
    public const string Enemy = "Enemy";
    public const string Interactable = "Interactable";
    public const string EnemyHitbox = "EnemyHitbox";
    public const string PlayerHitbox = "PlayerHitbox";
    public const string Environment = "Environment";
    public const string DynamicProp = "DynamicProp";

    // Fixed slots in the project's TagManager (written by the editor build step).
    public static readonly (int index, string name)[] Slots =
    {
        (8, Player), (9, Enemy), (10, Interactable), (11, EnemyHitbox), (12, PlayerHitbox), (13, Environment), (14, DynamicProp),
    };

    // Solid world geometry: what you stand on, walk into, and what blocks the camera and line of sight.
    public static int World => LayerMask.GetMask("Default", Environment);
    public static int EnemyHitboxes => LayerMask.GetMask(EnemyHitbox);
    public static int PlayerHitboxes => LayerMask.GetMask(PlayerHitbox);
    public static int Interactables => LayerMask.GetMask(Interactable);
}
