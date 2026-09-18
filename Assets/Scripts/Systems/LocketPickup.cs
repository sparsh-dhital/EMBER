using UnityEngine;

// The sacred locket. Taking it unlocks the one-time prayer.
public class LocketPickup : MonoBehaviour, IInteractable
{
    public PrayerSystem prayer;
    public GameObject visual;
    public ParticleSystem shimmer;
    public ParticleSystem pickupBurst;
    public float spinSpeed = 25f;

    bool taken;

    public string Prompt => taken ? null : "Take the sacred locket";
    public bool CanInteract => !taken;

    void Update()
    {
        if (!taken && visual) visual.transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    public void Interact(PlayerInteractor player)
    {
        if (taken) return;
        taken = true;

        AudioManager.Play(Sfx.LocketPickup);
        Haptics.Pulse(0.5f, 0.2f);
        if (shimmer) shimmer.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (pickupBurst) pickupBurst.Play();
        if (visual) visual.SetActive(false);
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;

        if (prayer) prayer.Unlock();
    }
}
