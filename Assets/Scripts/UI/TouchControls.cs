using TMPro;
using UnityEngine;

// Shows the on-screen controls on touch devices and makes the buttons contextual:
// Interact only appears near something usable, Pray only once you hold the locket.
public class TouchControls : MonoBehaviour
{
    public CanvasGroup group;
    public PlayerInteractor interactor;
    public PrayerSystem prayer;

    [Header("Buttons")]
    public CanvasGroup interactButton;
    public TMP_Text interactLabel;
    public CanvasGroup prayButton;
    public RectTransform prayGlow;
    public CanvasGroup attackButton;
    public TMP_Text attackLabel;
    public CanvasGroup blockButton;
    public RectTransform blockGlow;
    public TMP_Text crawlLabel;
    public PlayerController controller;

    [Tooltip("Show the touch controls in the Editor and on desktop, for testing.")]
    public bool forceShow;

    void Update()
    {
        var gm = GameManager.Instance;
        bool show = (InputReader.IsTouchDevice || forceShow) && (gm == null || gm.IsGameplayActive);
        SetGroup(group, show ? 1f : 0f, show);
        if (!show) return;

        bool canInteract = interactor && interactor.Current != null;
        SetGroup(interactButton, canInteract ? 1f : 0f, canInteract);
        if (canInteract && interactLabel)
        {
            var it = interactor.Current;
            interactLabel.text = it is RadioCentre ? "USE" : "TAKE";
        }

        if (crawlLabel && controller) crawlLabel.text = controller.IsCrawling ? "STAND" : "CRAWL";
        // The sword button only exists while a sword is carried.
        bool armed = SwordController.Armed;
        SetGroup(attackButton, armed ? 1f : 0f, armed);
        SetGroup(blockButton, armed ? 1f : 0f, armed);

        // The guard button lights up while it is actually holding a guard, so the
        // player can see the stance is live without watching the character.
        var sword = SwordController.Instance;
        if (blockGlow)
        {
            bool guarding = sword && sword.IsGuarding;
            blockGlow.gameObject.SetActive(armed && guarding);
            if (guarding) blockGlow.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 9f));
        }

        bool hasPrayer = prayer && prayer.HasLocket && !prayer.Used;
        bool ready = prayer && prayer.CanPray;
        SetGroup(prayButton, hasPrayer ? (ready ? 1f : 0.35f) : 0f, hasPrayer);
        if (prayGlow)
        {
            prayGlow.gameObject.SetActive(ready);
            if (ready) prayGlow.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 5f));
        }
    }

    static void SetGroup(CanvasGroup g, float alpha, bool interactable)
    {
        if (!g) return;
        g.alpha = Mathf.MoveTowards(g.alpha, alpha, Time.unscaledDeltaTime * 6f);
        g.blocksRaycasts = interactable;
        g.interactable = interactable;
    }
}
