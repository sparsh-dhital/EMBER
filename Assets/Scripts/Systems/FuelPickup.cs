using System.Collections.Generic;
using UnityEngine;

// A lantern oil can. Walk into it to refill the lantern.
[RequireComponent(typeof(Collider))]
public class FuelPickup : MonoBehaviour
{
    // Every fuel can still waiting in the level (the vampire spawner avoids these spots).
    public static readonly List<FuelPickup> Active = new List<FuelPickup>();

    [Tooltip("Fuel restored. 25 = a quarter of the lantern.")]
    public float fuelAmount = 25f;

    [Header("Idle Animation")]
    [Tooltip("The child that bobs and turns. Leave empty to animate this object.")]
    public Transform visual;
    public float bobHeight = 0.07f;
    public float bobSpeed = 1.6f;
    public float rotateSpeed = 30f;

    [Header("Feedback")]
    [Tooltip("Played where the can was, then left behind when the can disappears.")]
    public ParticleSystem pickupBurst;

    Vector3 startLocalPos;
    float phase;

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    void Start()
    {
        if (!visual) visual = transform;
        startLocalPos = visual.localPosition;
        phase = Random.value * 6.28f;
        GetComponent<Collider>().isTrigger = true;
    }

    void Update()
    {
        phase += Time.deltaTime * bobSpeed;
        visual.localPosition = startLocalPos + Vector3.up * (Mathf.Sin(phase) * bobHeight);
        visual.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        var fuel = other.GetComponentInParent<LanternFuel>();
        if (fuel == null) return;
        if (GameManager.Instance && !GameManager.Instance.IsGameplayActive) return;

        fuel.AddFuel(fuelAmount * GameConfig.Current.fuelPickupMultiplier);
        Haptics.Pulse(0.3f, 0.08f);

        if (pickupBurst)
        {
            pickupBurst.transform.SetParent(null, true);
            pickupBurst.Play();
            Destroy(pickupBurst.gameObject, 3f);
        }
        gameObject.SetActive(false);
    }
}
