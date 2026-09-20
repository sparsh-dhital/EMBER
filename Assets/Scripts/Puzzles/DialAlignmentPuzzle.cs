using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Bring every tuning dial round to the index mark.
///
/// The twist is that the dials are geared: turning one drags its neighbour along. That turns
/// what would be a trivial "click until zero" into a small ordering problem, because the
/// dial you fix last must be the one nothing else disturbs.
/// </summary>
public class DialAlignmentPuzzle : MonoBehaviour, IPuzzle
{
    public event Action Solved;

    class Ring
    {
        public int steps;
        public int position;          // 0 == aligned with the index
        public int coupling;          // how far the next ring is dragged per step, 0 = free
        public RectTransform pointer;
        public RectTransform face;
        public TMP_Text readout;
        public bool Aligned => position == 0;
    }

    Ring[] rings;
    PuzzleStyle style;
    TMP_Text status;
    bool finished;

    public void Build(RectTransform host, PuzzleRequest request, PuzzleStyle s)
    {
        style = s;
        var rng = new System.Random(request.seed);

        int count = request.tier == 0 ? 2 : request.tier == 1 ? 3 : 4;
        int steps = request.tier == 0 ? 8 : 12;

        rings = new Ring[count];
        float spacing = count <= 3 ? 190f : 152f;
        float originX = -(count - 1) * spacing * 0.5f;
        float faceSize = count <= 3 ? 150f : 126f;

        for (int i = 0; i < count; i++)
        {
            var ring = new Ring { steps = steps };
            // Easy leaves the dials independent; from Normal up they are geared together.
            ring.coupling = request.tier == 0 || i == count - 1 ? 0 : (i % 2 == 0 ? 1 : -1);
            do { ring.position = rng.Next(0, steps); } while (ring.position == 0);
            rings[i] = ring;

            Vector2 centre = new Vector2(originX + i * spacing, 22f);

            PuzzleWidgets.Sprite("Face" + i, host, s.circle, new Color(1f, 1f, 1f, 0.05f),
                centre, new Vector2(faceSize, faceSize));
            PuzzleWidgets.Sprite("Rim" + i, host, s.ring, new Color(1f, 1f, 1f, 0.18f),
                centre, new Vector2(faceSize, faceSize));

            // The index mark the pointer has to meet, at twelve o'clock.
            PuzzleWidgets.Sprite("Index" + i, host, s.bar, s.warm,
                centre + new Vector2(0f, faceSize * 0.5f + 7f), new Vector2(4f, 14f), sliced: true);

            // The rotating face carries the pointer.
            var face = PuzzleWidgets.Rect("Dial" + i, host);
            PuzzleWidgets.Place(face, centre, new Vector2(faceSize, faceSize));
            ring.face = face;

            var pointer = PuzzleWidgets.Sprite("Pointer" + i, face, s.bar, s.cream,
                new Vector2(0f, faceSize * 0.24f), new Vector2(5f, faceSize * 0.42f), sliced: true);
            ring.pointer = (RectTransform)pointer.transform;

            PuzzleWidgets.Sprite("Hub" + i, host, s.circle, s.dim, centre, new Vector2(16f, 16f));

            int index = i;
            PuzzleWidgets.Button("Left" + i, host, s, "↺",
                centre + new Vector2(-faceSize * 0.5f - 26f, -faceSize * 0.5f - 22f),
                new Vector2(44f, 40f), () => Turn(index, -1), 22f);
            PuzzleWidgets.Button("Right" + i, host, s, "↻",
                centre + new Vector2(faceSize * 0.5f + 26f, -faceSize * 0.5f - 22f),
                new Vector2(44f, 40f), () => Turn(index, +1), 22f);

            ring.readout = PuzzleWidgets.Label("Read" + i, host, s, "",
                14f, s.dim, centre + new Vector2(0f, -faceSize * 0.5f - 24f),
                new Vector2(140f, 20f));
        }

        string gearing = count > 1 && rings[0].coupling != 0
            ? "The dials are geared — turning one drags the next"
            : "Turn each dial to its index mark";
        PuzzleWidgets.Label("Note", host, s, gearing, 15f, s.dim,
            new Vector2(0f, 142f), new Vector2(600f, 22f));

        status = PuzzleWidgets.Label("Status", host, s, "", 17f, s.dim,
            new Vector2(0f, -162f), new Vector2(560f, 24f));

        Refresh();
    }

    void Turn(int index, int direction)
    {
        if (finished) return;

        var ring = rings[index];
        ring.position = Wrap(ring.position + direction, ring.steps);

        // Gearing: this dial drags the one to its right.
        if (ring.coupling != 0 && index + 1 < rings.Length)
        {
            var next = rings[index + 1];
            next.position = Wrap(next.position + direction * ring.coupling, next.steps);
        }

        Refresh();
    }

    static int Wrap(int value, int steps) => ((value % steps) + steps) % steps;

    void Refresh()
    {
        int aligned = 0;
        foreach (var ring in rings)
        {
            float angle = -ring.position / (float)ring.steps * 360f;
            if (ring.face) ring.face.localRotation = Quaternion.Euler(0f, 0f, angle);

            bool ok = ring.Aligned;
            if (ok) aligned++;
            if (ring.pointer)
                ring.pointer.GetComponent<UnityEngine.UI.Image>().color = ok ? style.good : style.cream;
            if (ring.readout)
            {
                // Shortest way round, so the readout tells you which button to press.
                int offset = ring.position > ring.steps / 2 ? ring.position - ring.steps : ring.position;
                ring.readout.text = ok ? "ALIGNED" : (offset > 0 ? "↺ " : "↻ ") + Mathf.Abs(offset);
                ring.readout.color = ok ? style.good : style.dim;
            }
        }

        if (status)
        {
            status.text = aligned + " of " + rings.Length + " aligned";
            status.color = aligned == rings.Length ? style.good : style.dim;
        }

        if (aligned == rings.Length && !finished)
        {
            finished = true;
            Solved?.Invoke();
        }
    }

    public bool RevealHint()
    {
        // Free the gearing on one coupled dial, turning the ordering problem back into a
        // simple one. That is a real concession rather than just showing a number.
        foreach (var ring in rings)
        {
            if (ring.coupling == 0) continue;
            ring.coupling = 0;
            Refresh();
            return true;
        }
        return false;
    }

    public void Dispose() { }
}
