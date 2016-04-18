using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour {

    private GameObject panel;
    private Image fadeImage;
    private Text text;
    private bool loading = false;

	private AudioSource death_sfx;
    // Use this for initialization
    void Start() {
		death_sfx = GameObject.FindGameObjectWithTag ("Player").GetComponent<AudioSource> ();
        panel = transform.Find("Panel").gameObject;
        if (panel) {
            fadeImage = panel.GetComponent<Image>();
            text = panel.transform.Find("Text").GetComponent<Text>();
            text.gameObject.SetActive(false);
        }

        loading = true;
        StartCoroutine(fade(true, false, 1.0f));
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            Application.Quit();
        }
    }

    public void playDeathSequence() {
		death_sfx.Play ();
        if (loading) {
            return;
        }
        text.gameObject.SetActive(true);
        loading = true;
        StartCoroutine(fadeOutReloadRoutine());

    }
    private IEnumerator fadeOutReloadRoutine() {
        yield return fade(false, true, 1.0f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // fades in from whatever color the image is at
    private IEnumerator fade(bool fadein, bool pauseGame, float time) {
        if (!fadein) {
            panel.SetActive(true);
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
            panel.SetActive(false);
        }
        loading = false;
    }

}
