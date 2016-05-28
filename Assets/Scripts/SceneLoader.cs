using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour {

    private GameObject fadePanel;
    private Image fadeImage;
    private Text text;
    private bool loading = false;

    // Use this for initialization
    void Start() {
        fadePanel = transform.Find("FadePanel").gameObject;
        if (fadePanel) {
            fadeImage = fadePanel.GetComponent<Image>();
            text = fadePanel.transform.Find("GameText").GetComponent<Text>();
            text.gameObject.SetActive(false);
        }

        loading = true;
        StartCoroutine(fade(true, false, 1.0f));
    }

    public void fadeOutWithText(string t) {
        if (loading) {
            return;
        }
        text.gameObject.SetActive(true);
        text.text = t;
        loading = true;
        StartCoroutine(fadeOutReloadRoutine());
    }

    private IEnumerator fadeOutReloadRoutine() {
        yield return fade(false, false, 2.0f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // fades in from whatever color the image is at
    private IEnumerator fade(bool fadein, bool pauseGame, float time) {
        if (!fadein) {
            fadePanel.SetActive(true);
        }
        if (pauseGame) {
            Time.timeScale = 0.0f;
        }

        float endTime = Time.realtimeSinceStartup + time;
        while (Time.realtimeSinceStartup < endTime) {
            float t = Time.realtimeSinceStartup;
            Color c = fadeImage.color;
            if (fadein) {
                c.a = (endTime - t) / time;
            } else {
                c.a = 1.0f - (endTime - t) / time;
            }
            fadeImage.color = c;
            yield return null;
        }

        if (pauseGame) {
            Time.timeScale = 1.0f;
        }

        // reset fade variables back to defaults
        fadeImage.color = Color.black;
        if (fadein) {
            fadePanel.SetActive(false);
        }
        loading = false;
    }

}
