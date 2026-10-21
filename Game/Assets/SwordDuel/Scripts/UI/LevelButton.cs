using UnityEngine;
using UnityEngine.UI;

// En gumb na zaslonu za izbiro levela: zaklenjen (ključavnica), odklenjen ali dokončan (kljukica).
public class LevelButton : MonoBehaviour
{
    public int levelNumber = 1;
    public Button button;
    public Text numberText;
    public Text nameText;
    public Image lockIcon;
    public Image checkIcon;

    void Awake()
    {
        button.onClick.AddListener(() =>
        {
            if (SaveManager.IsUnlocked(levelNumber)) GameSession.StartLevel(levelNumber);
        });
    }

    public void Refresh()
    {
        bool unlocked = SaveManager.IsUnlocked(levelNumber);
        bool completed = SaveManager.IsCompleted(levelNumber);
        button.interactable = unlocked;
        lockIcon.gameObject.SetActive(!unlocked);
        checkIcon.gameObject.SetActive(completed);
        numberText.gameObject.SetActive(unlocked);

        var level = LevelDatabase.Instance.Get(levelNumber);
        nameText.text = unlocked ? level.enemyName : "Locked";
    }
}
