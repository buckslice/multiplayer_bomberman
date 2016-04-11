using UnityEngine;
using System.Collections;

public class SceneSwap : MonoBehaviour {

	public void ToGame() {
		UnityEngine.SceneManagement.SceneManager.LoadScene(1);
	}

	public void ToMenu() {
		UnityEngine.SceneManagement.SceneManager.LoadScene(0);
	}
}
