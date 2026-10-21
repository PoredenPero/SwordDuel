using UnityEngine;

// Shranjevanje napredka v PlayerPrefs (lokalno na računalniku).
public static class SaveManager
{
    const string CoinsKey = "coins";
    const string UnlockedKey = "highestUnlocked";
    const string CompletedKey = "completed_";
    const string StrengthKey = "strengthLevel";
    const string SpeedKey = "speedLevel";
    const string VolumeKey = "volume";
    const string SensitivityKey = "sensitivity";

    public static int Coins
    {
        get => PlayerPrefs.GetInt(CoinsKey, 0);
        set { PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    // Najvišji odklenjen level (na začetku je odklenjen samo level 1)
    public static int HighestUnlocked
    {
        get => Mathf.Max(1, PlayerPrefs.GetInt(UnlockedKey, 1));
        set { PlayerPrefs.SetInt(UnlockedKey, value); PlayerPrefs.Save(); }
    }

    public static bool IsUnlocked(int level) => level <= HighestUnlocked;

    public static bool IsCompleted(int level) => PlayerPrefs.GetInt(CompletedKey + level, 0) == 1;

    public static void MarkCompleted(int level)
    {
        PlayerPrefs.SetInt(CompletedKey + level, 1);
        if (HighestUnlocked < level + 1) PlayerPrefs.SetInt(UnlockedKey, level + 1);
        PlayerPrefs.Save();
    }

    public static int StrengthLevel
    {
        get => PlayerPrefs.GetInt(StrengthKey, 0);
        set { PlayerPrefs.SetInt(StrengthKey, value); PlayerPrefs.Save(); }
    }

    public static int SpeedLevel
    {
        get => PlayerPrefs.GetInt(SpeedKey, 0);
        set { PlayerPrefs.SetInt(SpeedKey, value); PlayerPrefs.Save(); }
    }

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
        set { PlayerPrefs.SetFloat(VolumeKey, value); PlayerPrefs.Save(); AudioListener.volume = value; }
    }

    public static float Sensitivity
    {
        get => PlayerPrefs.GetFloat(SensitivityKey, 1f);
        set { PlayerPrefs.SetFloat(SensitivityKey, value); PlayerPrefs.Save(); }
    }

    // Izbriše ves napredek (gumb v nastavitvah)
    public static void ResetProgress()
    {
        float volume = Volume, sensitivity = Sensitivity;
        PlayerPrefs.DeleteAll();
        Volume = volume;
        Sensitivity = sensitivity;
    }
}
