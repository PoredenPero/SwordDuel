using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Črn zaslon, ki se zatemni pred menjavo scene in posvetli po njej (mehak prehod).
public class ScreenFader : MonoBehaviour
{
    const float FadeTime = 0.35f;

    static ScreenFader instance;
    CanvasGroup group;
    bool busy;

    static ScreenFader Get()
    {
        if (instance != null) return instance;

        var go = new GameObject("ScreenFader");
        DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        go.AddComponent<GraphicRaycaster>();
        instance = go.AddComponent<ScreenFader>();
        instance.group = go.AddComponent<CanvasGroup>();
        instance.group.alpha = 0f;
        instance.group.blocksRaycasts = false;

        var image = new GameObject("Black").AddComponent<Image>();
        image.transform.SetParent(go.transform, false);
        image.color = Color.black;
        var rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return instance;
    }

    public static void FadeAndLoad(string scene)
    {
        var fader = Get();
        if (!fader.busy) fader.StartCoroutine(fader.Run(scene));
    }

    IEnumerator Run(string scene)
    {
        busy = true;
        group.blocksRaycasts = true;
        yield return Fade(0f, 1f);
        yield return SceneManager.LoadSceneAsync(scene);
        yield return null;
        yield return Fade(1f, 0f);
        group.blocksRaycasts = false;
        busy = false;
    }

    IEnumerator Fade(float from, float to)
    {
        for (float t = 0; t < FadeTime; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / FadeTime);
            yield return null;
        }
        group.alpha = to;
    }
}
