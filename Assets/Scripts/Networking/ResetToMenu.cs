using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class ResetToMenu : MonoBehaviour {

    private static bool queuedReset = false;

    void LateUpdate() {
        bool reset = false;
        if (Input.GetKeyDown(KeyCode.Escape)) {
            // if in menu then quit game
            if (SceneManager.GetActiveScene().buildIndex == 0) {
                Application.Quit();
            }
            reset = true;
        } else if(queuedReset) {
            queuedReset = false;
            reset = true;
        }

        // resets game and networking back to menu state
        if (reset) {
            NetworkTransport.Shutdown();
            GameObject netGO = GameObject.Find("Networking");
            if (netGO) {
                Destroy(netGO);
            }
            SceneManager.LoadScene(0);

            Debug.Log("<<< GAME AND NETWORK RESET >>>");
        }

    }

    // queues a reset to happen (should only be done in LateUpdate to avoid errors)
    public static void Reset() {
        queuedReset = true;
    }

}