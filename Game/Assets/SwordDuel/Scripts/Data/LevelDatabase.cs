using UnityEngine;

// Seznam vseh levelov. Leži v mapi Resources, da ga lahko vsaka scena naloži z LevelDatabase.Instance.
[CreateAssetMenu(menuName = "Sword Duel/Level Database", fileName = "LevelDatabase")]
public class LevelDatabase : ScriptableObject
{
    public LevelData[] levels;

    static LevelDatabase instance;
    public static LevelDatabase Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<LevelDatabase>("LevelDatabase");
            return instance;
        }
    }

    public int Count => levels.Length;

    // levelNumber se začne z 1
    public LevelData Get(int levelNumber)
    {
        int i = Mathf.Clamp(levelNumber - 1, 0, levels.Length - 1);
        return levels[i];
    }
}
