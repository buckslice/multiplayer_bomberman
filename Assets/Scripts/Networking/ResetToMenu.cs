using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class ResetToMenu : MonoBehaviour {

	// Update is called once per frame
	void LateUpdate () {
        // if in main game scene and hit escape then quit
        // back to menu screen and reset networking
        if (SceneManager.GetActiveScene().buildIndex == 1 &&
            Input.GetKeyDown(KeyCode.Escape)) {

            NetworkTransport.Shutdown();
            Destroy(gameObject);
            SceneManager.LoadScene(0);

            Debug.Log("<<< GAME AND NETWORK RESET >>>");
        }
    }
}
