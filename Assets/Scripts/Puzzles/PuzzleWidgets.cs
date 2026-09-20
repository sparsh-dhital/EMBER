using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Small factory for the controls the puzzle boards are built from. Keeping them here means
/// every board shares one visual language and one set of interaction rules, and an individual
/// puzzle stays readable as logic rather than layout code.
/// </summary>
public static class PuzzleWidgets
{
    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>Places a rect with an explicit centre offset and size, anchored to the middle.</summary>
    public static RectTransform Place(RectTransform r, Vector2 centre, Vector2 size)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = centre;
        r.sizeDelta = size;
        return r;
    }

    public static Image Panel(string name, Transform parent, PuzzleStyle s, Color colour,
                              Vector2 centre, Vector2 size)
    {
        var img = Rect(name, parent).gameObject.AddComponent<Image>();
        img.sprite = s.panel;
        img.type = Image.Type.Sliced;
        img.color = colour;
        img.raycastTarget = false;
        Place((RectTransform)img.transform, centre, size);
        return img;
    }

    public static Image Sprite(string name, Transform parent, Sprite sprite, Color colour,
                              Vector2 centre, Vector2 size, bool sliced = false)
    {
        var img = Rect(name, parent).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        if (sliced) img.type = Image.Type.Sliced;
        img.color = colour;
        img.raycastTarget = false;
        Place((RectTransform)img.transform, centre, size);
        return img;
    }

    public static TMP_Text Label(string name, Transform parent, PuzzleStyle s, string text,
                                 float size, Color colour, Vector2 centre, Vector2 box,
                                 TextAlignmentOptions align = TextAlignmentOptions.Center,
                                 bool bold = false)
    {
        var go = Rect(name, parent).gameObject;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = bold ? s.semibold : s.regular;
        t.text = text;
        t.fontSize = size;
        t.color = colour;
        t.alignment = align;
        t.raycastTarget = false;
        t.richText = false;
        // Fonts are baked static, so leave auto-sizing off: it would ask for point sizes the
        // atlas does not contain and reintroduce blurry text.
        t.enableAutoSizing = false;
        Place((RectTransform)go.transform, centre, box);
        return t;
    }

    /// <summary>
    /// A clickable cell. Reports presses through <paramref name="onClick"/> and exposes its
    /// background and label so the owning puzzle can restyle it as state changes.
    /// </summary>
    public static PuzzleButton Button(string name, Transform parent, PuzzleStyle s, string text,
                                      Vector2 centre, Vector2 size, Action onClick,
                                      float fontSize = 22f)
    {
        var r = Rect(name, parent);
        Place(r, centre, size);

        var bg = r.gameObject.AddComponent<Image>();
        bg.sprite = s.panel;
        bg.type = Image.Type.Sliced;
        bg.color = s.inkSlot;
        bg.raycastTarget = true;

        var label = Label("Label", r, s, text, fontSize, s.cream, Vector2.zero, size, bold: true);

        var btn = r.gameObject.AddComponent<PuzzleButton>();
        btn.Init(bg, label, onClick);
        return btn;
    }
}

/// <summary>
/// A puzzle cell that responds on press rather than release, so boards feel immediate on
/// both mouse and touch, and hovers so desktop players get the same read as a gamepad focus.
/// </summary>
public class PuzzleButton : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image Background { get; private set; }
    public TMP_Text Label { get; private set; }

    Action onClick;
    Color resting;
    bool hovered;
    bool interactable = true;

    public void Init(Image background, TMP_Text label, Action click)
    {
        Background = background;
        Label = label;
        onClick = click;
        resting = background.color;
    }

    /// <summary>Changes the cell's resting colour, preserving any hover highlight.</summary>
    public void SetResting(Color colour)
    {
        resting = colour;
        Refresh();
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
        if (Background) Background.raycastTarget = value;
        Refresh();
    }

    void Refresh()
    {
        if (!Background) return;
        Color c = resting;
        if (!interactable) c.a *= 0.5f;
        else if (hovered) c = Color.Lerp(c, Color.white, 0.18f);
        Background.color = c;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (!interactable) return;
        AudioManager.Play(Sfx.PuzzleClick, 0.8f);
        transform.localScale = Vector3.one * 0.95f;
        onClick?.Invoke();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (!interactable) return;
        hovered = true;
        Refresh();
        AudioManager.Play(Sfx.UiHover, 0.35f);
    }

    public void OnPointerExit(PointerEventData e)
    {
        hovered = false;
        transform.localScale = Vector3.one;
        Refresh();
    }

    void Update()
    {
        // Ease the press-down back out; unscaled because the game is paused behind the board.
        if (transform.localScale.x < 1f)
            transform.localScale = Vector3.one * Mathf.MoveTowards(transform.localScale.x, 1f,
                                                                   Time.unscaledDeltaTime * 1.2f);
    }
}
