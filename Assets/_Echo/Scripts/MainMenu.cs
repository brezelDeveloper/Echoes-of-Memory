using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("씬")]
    [SerializeField] private string gameSceneName = "InGame";

    [Header("암전 연출")]
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private float fadeOutTime = 1f;
    [SerializeField] private float blackHoldTime = 0.5f;
    [SerializeField] private float fadeInTime = 1.5f;

    [Header("소리")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;

    private bool started;

    void Start()
    {
        if (fade != null)
        {
            fade.alpha = 0f;
            fade.blocksRaycasts = false;
        }
    }

    public void Play()
    {
        if (started)
        {
            return;
        }

        started = true;
        StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        GameObject fadeRoot = fade != null ? fade.transform.root.gameObject : null;

        if (fade != null)
        {
            fade.blocksRaycasts = true;
            yield return Fade(0f, 1f, fadeOutTime);
            DontDestroyOnLoad(fadeRoot);
        }

        DontDestroyOnLoad(gameObject);
        yield return SceneManager.LoadSceneAsync(gameSceneName);

        if (audioSource != null && openSound != null)
        {
            audioSource.PlayOneShot(openSound);
            yield return new WaitForSeconds(openSound.length);
        }

        yield return new WaitForSeconds(blackHoldTime);

        if (fade != null)
        {
            yield return Fade(1f, 0f, fadeInTime);
            Destroy(fadeRoot);
        }

        Destroy(gameObject);
    }

    IEnumerator Fade(float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            fade.alpha = Mathf.Lerp(from, to, t / time);
            yield return null;
        }

        fade.alpha = to;
    }

    public void Settings()
    {
        Debug.Log("설정 화면은 준비 중이에요");
    }

    public void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}