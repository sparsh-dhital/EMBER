using System;

public enum GameState { MainMenu, Playing, Paused, PrayerEmergency, Victory, Defeat }

// How strong the lantern is. Vampires, audio and screen effects all react to this.
public enum LightBand { Strong, Medium, Critical, Out }

// A small event hub for the big moments of a run.
// Systems raise events here and anything can listen, so the HUD, audio and vampires
// don't need direct references to each other.
// Always unsubscribe in OnDisable, because these events are static.
public static class GameEvents
{
    public static event Action<GameState> StateChanged;
    public static event Action<float> FuelChanged;
    public static event Action<float> FuelCollected;
    public static event Action FuelEmpty;
    public static event Action LanternRelit;
    public static event Action<LightBand> LightBandChanged;
    public static event Action<int, int> RadioPartCollected;
    public static event Action AllRadioPartsCollected;
    public static event Action LocketCollected;
    public static event Action PrayerStarted;
    public static event Action PrayerEnded;
    public static event Action RadioCallStarted;
    public static event Action MissionCompleted;
    public static event Action<float> PlayerDamaged;
    public static event Action PlayerDied;
    public static event Action PlayerWon;
    public static event Action<string, string, float> MessageRequested;

    public static void RaiseStateChanged(GameState s) => StateChanged?.Invoke(s);
    public static void RaiseFuelChanged(float fraction) => FuelChanged?.Invoke(fraction);
    public static void RaiseFuelCollected(float amount) => FuelCollected?.Invoke(amount);
    public static void RaiseFuelEmpty() => FuelEmpty?.Invoke();
    public static void RaiseLanternRelit() => LanternRelit?.Invoke();
    public static void RaiseLightBandChanged(LightBand band) => LightBandChanged?.Invoke(band);
    public static void RaiseRadioPartCollected(int collected, int required) => RadioPartCollected?.Invoke(collected, required);
    public static void RaiseAllRadioPartsCollected() => AllRadioPartsCollected?.Invoke();
    public static void RaiseLocketCollected() => LocketCollected?.Invoke();
    public static void RaisePrayerStarted() => PrayerStarted?.Invoke();
    public static void RaisePrayerEnded() => PrayerEnded?.Invoke();
    public static void RaiseRadioCallStarted() => RadioCallStarted?.Invoke();
    public static void RaiseMissionCompleted() => MissionCompleted?.Invoke();
    public static void RaisePlayerDamaged(float amount) => PlayerDamaged?.Invoke(amount);
    public static void RaisePlayerDied() => PlayerDied?.Invoke();
    public static void RaisePlayerWon() => PlayerWon?.Invoke();

    // Big centred HUD message: title, optional subtitle, seconds on screen.
    public static void ShowMessage(string title, string subtitle = "", float seconds = 3f) => MessageRequested?.Invoke(title, subtitle, seconds);
}
