using UnityEngine;
using UnityEngine.SceneManagement;

// Prenaša izbrani level iz glavnega menija v sceno Game in skrbi za prehode med scenami.
public static class GameSession
{
    public const string MainMenuScene = "MainMenu";
    public const string GameScene = "Game";

    public static int SelectedLevel = 1;

    // Ko se vrnemo v meni, naj se odpre izbira levelov namesto glavnega zaslona
    public static bool OpenLevelSelectOnMenu;

    public static void StartLevel(int levelNumber)
    {
        SelectedLevel = levelNumber;
        Load(GameScene);
    }

    public static void GoToMenu(bool openLevelSelect = false)
    {
        OpenLevelSelectOnMenu = openLevelSelect;
        Load(MainMenuScene);
    }

    static void Load(string scene)
    {
        Time.timeScale = 1f;
        ScreenFader.FadeAndLoad(scene);
    }
}
