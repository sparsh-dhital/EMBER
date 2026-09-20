using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Set the valve switches so every line on the service plate reads true.
///
/// This is the deduction puzzle of the set: no feedback beyond the printed rules, which
/// are generated against a hidden solution so the board is always satisfiable, and always
/// solvable by reasoning rather than by trying all combinations.
/// </summary>
public class ValveLogicPuzzle : MonoBehaviour, IPuzzle
{
    public event Action Solved;

    abstract class Rule
    {
        public abstract bool Holds(bool[] state);
        public abstract string Text(string[] names);
        public TMP_Text label;
    }

    /// <summary>"Valve B is open." / "Valve B is shut."</summary>
    class FixedRule : Rule
    {
        public int index; public bool wanted;
        public override bool Holds(bool[] s) => s[index] == wanted;
        public override string Text(string[] n) => "Valve " + n[index] + " is " + (wanted ? "OPEN" : "SHUT");
    }

    /// <summary>"Valves A and C match." / "differ."</summary>
    class PairRule : Rule
    {
        public int a, b; public bool same;
        public override bool Holds(bool[] s) => (s[a] == s[b]) == same;
        public override string Text(string[] n) =>
            "Valves " + n[a] + " and " + n[b] + (same ? " MATCH" : " DIFFER");
    }

    /// <summary>"Exactly N valves are open."</summary>
    class CountRule : Rule
    {
        public int count;
        public override bool Holds(bool[] s)
        {
            int open = 0;
            foreach (bool v in s) if (v) open++;
            return open == count;
        }
        public override string Text(string[] n) => "Exactly " + count + " valve" + (count == 1 ? " is" : "s are") + " OPEN";
    }

    static readonly string[] Names = { "A", "B", "C", "D", "E", "F" };

    bool[] state;
    bool[] solution;
    PuzzleButton[] switches;
    readonly List<Rule> rules = new List<Rule>();
    PuzzleStyle style;
    TMP_Text status;
    bool finished;
    int hintsUsed;

    public void Build(RectTransform host, PuzzleRequest request, PuzzleStyle s)
    {
        style = s;
        var rng = new System.Random(request.seed);

        int count = request.tier == 0 ? 3 : request.tier == 1 ? 4 : 5;
        state = new bool[count];
        solution = new bool[count];
        for (int i = 0; i < count; i++) solution[i] = rng.Next(0, 2) == 1;

        BuildRules(rng, count, request.tier);

        // Start from a configuration that is not already correct.
        for (int i = 0; i < count; i++) state[i] = rng.Next(0, 2) == 1;
        if (Satisfied()) state[0] = !state[0];

        PuzzleWidgets.Label("Plate", host, s, "SERVICE PLATE", 15f, s.dim,
            new Vector2(0f, 168f), new Vector2(560f, 22f));

        for (int i = 0; i < rules.Count; i++)
        {
            rules[i].label = PuzzleWidgets.Label("Rule" + i, host, s,
                "•  " + rules[i].Text(Names), 18f, s.cream,
                new Vector2(0f, 132f - i * 32f), new Vector2(560f, 26f));
        }

        float spacing = 104f;
        float originX = -(count - 1) * spacing * 0.5f;
        float switchY = 132f - rules.Count * 32f - 46f;

        switches = new PuzzleButton[count];
        for (int i = 0; i < count; i++)
        {
            int index = i;
            switches[i] = PuzzleWidgets.Button("Valve" + i, host, s, Names[i],
                new Vector2(originX + i * spacing, switchY), new Vector2(84f, 96f),
                () => Toggle(index), 26f);
        }

        status = PuzzleWidgets.Label("Status", host, s, "", 17f, s.dim,
            new Vector2(0f, switchY - 76f), new Vector2(560f, 24f));

        Refresh();
    }

    // Rules are written against the hidden solution, so they are guaranteed consistent.
    // Harder tiers state fewer facts outright and lean on relational clues instead.
    void BuildRules(System.Random rng, int count, int tier)
    {
        int fixedCount = tier == 0 ? 2 : tier == 1 ? 1 : 1;
        var used = new HashSet<int>();

        for (int i = 0; i < fixedCount; i++)
        {
            int idx = PickUnused(rng, count, used);
            rules.Add(new FixedRule { index = idx, wanted = solution[idx] });
        }

        int pairCount = tier == 0 ? 1 : tier == 1 ? 2 : 3;
        for (int i = 0; i < pairCount; i++)
        {
            int a = rng.Next(count), b = rng.Next(count);
            int guard = 0;
            while (b == a && guard++ < 20) b = rng.Next(count);
            if (a == b) continue;
            rules.Add(new PairRule { a = a, b = b, same = solution[a] == solution[b] });
        }

        // A count rule ties the whole board together and makes the deduction close.
        int open = 0;
        foreach (bool v in solution) if (v) open++;
        rules.Add(new CountRule { count = open });
    }

    static int PickUnused(System.Random rng, int count, HashSet<int> used)
    {
        for (int guard = 0; guard < 40; guard++)
        {
            int i = rng.Next(count);
            if (used.Add(i)) return i;
        }
        return 0;
    }

    void Toggle(int index)
    {
        if (finished) return;
        state[index] = !state[index];
        Refresh();
    }

    bool Satisfied()
    {
        foreach (var r in rules) if (!r.Holds(state)) return false;
        return true;
    }

    void Refresh()
    {
        for (int i = 0; i < switches.Length; i++)
        {
            bool open = state[i];
            if (switches[i] == null) continue;
            switches[i].SetResting(open ? new Color(1f, 0.72f, 0.36f, 0.26f) : style.inkSlot);
            if (switches[i].Label)
            {
                switches[i].Label.text = Names[i] + "\n" + (open ? "OPEN" : "SHUT");
                switches[i].Label.fontSize = 20f;
                switches[i].Label.color = open ? style.warm : style.dim;
            }
        }

        int met = 0;
        foreach (var r in rules)
        {
            bool ok = r.Holds(state);
            if (ok) met++;
            // Satisfied lines dim out, so the player can see what is left to reconcile.
            if (r.label) r.label.color = ok ? style.good : style.cream;
        }

        bool solved = met == rules.Count;
        if (status)
        {
            status.text = solved ? "PLATE SATISFIED" : met + " of " + rules.Count + " conditions met";
            status.color = solved ? style.good : style.dim;
        }

        if (solved && !finished)
        {
            finished = true;
            Solved?.Invoke();
        }
    }

    public bool RevealHint()
    {
        // Set one valve to its solved position and take it out of play.
        for (int i = hintsUsed; i < state.Length; i++)
        {
            if (state[i] == solution[i] && switches[i] != null && switches[i].enabled) continue;
            state[i] = solution[i];
            hintsUsed = i + 1;
            if (switches[i]) switches[i].SetInteractable(false);
            Refresh();
            return true;
        }
        return false;
    }

    public void Dispose() { }
}
