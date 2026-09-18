using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FuelPickup : MonoBehaviour
{
    public float fuelAmount = 30f;
    public float bobHeight = 0.16f;
    public float bobSpeed = 2f;
    public float rotateSpeed = 120f;

    Vector3 startPos;
    float phase;

    void Start()
    {
        startPos = transform.position;
        phase = Random.value * 6.28f;
        GetComponent<Collider>().isTrigger = true;
    }

    void Update()
    {
        phase += Time.deltaTime * bobSpeed;
        transform.position = startPos + Vector3.up * Mathf.Sin(phase) * bobHeight;
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        var fuel = other.GetComponentInParent<LanternFuel>();
        if (fuel == null) return;
        fuel.AddFuel(fuelAmount);
        Destroy(gameObject);
    }
}
