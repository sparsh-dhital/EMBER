using UnityEngine;

public static class TwoBoneIK
{
    // Analytic two-bone IK: bends the middle joint (knee/elbow) so the end bone reaches the target, keeping the existing bend plane.
    public static void Solve(Transform upper, Transform lower, Transform end, Vector3 target)
    {
        Vector3 a = upper.position, b = lower.position, c = end.position;
        float lab = Vector3.Distance(a, b), lcb = Vector3.Distance(b, c);
        float lat = Mathf.Clamp(Vector3.Distance(a, target), 0.01f, lab + lcb - 0.001f);

        float acab0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((c - a).normalized, (b - a).normalized), -1f, 1f));
        float babc0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((a - b).normalized, (c - b).normalized), -1f, 1f));
        float acat0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((c - a).normalized, (target - a).normalized), -1f, 1f));
        float acab1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
        float babc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

        Vector3 axis0 = Vector3.Cross(c - a, b - a);
        if (axis0.sqrMagnitude < 1e-8f) axis0 = upper.right;
        axis0.Normalize();
        Vector3 axis1 = Vector3.Cross(c - a, target - a);
        if (axis1.sqrMagnitude < 1e-8f) axis1 = axis0;
        axis1.Normalize();

        Quaternion aGlobal = upper.rotation, bGlobal = lower.rotation;
        Quaternion r0 = Quaternion.AngleAxis((acab1 - acab0) * Mathf.Rad2Deg, Quaternion.Inverse(aGlobal) * axis0);
        Quaternion r1 = Quaternion.AngleAxis((babc1 - babc0) * Mathf.Rad2Deg, Quaternion.Inverse(bGlobal) * axis0);
        Quaternion r2 = Quaternion.AngleAxis(acat0 * Mathf.Rad2Deg, Quaternion.Inverse(aGlobal) * axis1);
        upper.localRotation *= r0 * r2;
        lower.localRotation *= r1;
    }
}
