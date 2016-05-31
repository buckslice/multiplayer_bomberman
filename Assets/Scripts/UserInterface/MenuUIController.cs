using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MenuUIController : MonoBehaviour {

    // menu screen stuff
    public GameObject startClientButton;
    public GameObject joinButton;
    public InputField nameInputField;
    public InputField passwordInputField;
    public Text statusText;

    private IEnumerator statusTextAnim;

    private GameClient client;

    // Use this for initialization
    void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
        if (!client) {
            return;
        }

        // hit tab to switch between name and password input fields
        if (Input.GetKeyDown(KeyCode.Tab)) {
            if (nameInputField.isFocused) {
                passwordInputField.ActivateInputField();
            } else {
                nameInputField.ActivateInputField();
            }
        }

        // hit enter to try to join game with current credentials
        if (Input.GetKeyDown(KeyCode.Return)) {
            tryLoginWithInputs();
        }
    }

    public void tryLoginWithInputs() {
        if (nameInputField.text == "" || passwordInputField.text == "") {
            setStatusText("Enter name and password", Color.red, true);
            Debug.Log("CLIENT: no name/password entered");
        } else {
            client.tryLogin(nameInputField.text, passwordInputField.text);
        }
    }

    public void setupStartingUI(GameClient client) {
        this.client = client;

        // UI stuff
        startClientButton.SetActive(false);
        statusText.gameObject.SetActive(true);

        statusTextAnim = statusTextAnimRoutine();
        StartCoroutine(statusTextAnim);
    }

    public void stopStatusTextAnim() {
        StopCoroutine(statusTextAnim);
    }

    public void setupLoginUI() {
        setStatusText("Enter Login Info:", Color.green, true);
        nameInputField.gameObject.SetActive(true);
        passwordInputField.gameObject.SetActive(true);
        joinButton.SetActive(true);
    }

    // sets status text and color with optional flash animation
    public void setStatusText(string text, Color color, bool flash) {
        statusText.text = text;
        if (flash) {
            flashStatusText(color);
        } else {
            statusText.color = color;
        }
    }
    
    private IEnumerator flashStatusTextHandle;
    public void flashStatusText(Color color) {
        if (flashStatusTextHandle != null) {
            StopCoroutine(flashStatusTextHandle);
        }
        flashStatusTextHandle = flashStatusTextRoutine(color);
        StartCoroutine(flashStatusTextHandle);
    }

    // makes status text become this color but flashes white first
    private IEnumerator flashStatusTextRoutine(Color c) {
        float t = 0.0f;
        while (t < 1.0f) {
            t += Time.deltaTime;
            statusText.color = Color.Lerp(Color.white, c, t);
            yield return null;
        }
        statusText.color = c;
        flashStatusTextHandle = null;
    }

    private IEnumerator statusTextAnimRoutine() {
        int dots = 3;
        float timestep = 1.0f;
        float t = 0.0f;

        while (true) {
            t += Time.deltaTime;

            if (t > timestep) {
                dots = (dots + 1) % 4;
                string ds = "";
                for (int i = 0; i < dots; i++) {
                    ds += ".";
                }
                statusText.text = "Looking for Server" + ds;

                t -= timestep;
            }
            statusText.color = Color.Lerp(Color.yellow, Color.green, t);
            yield return null;
        }
    }

}
