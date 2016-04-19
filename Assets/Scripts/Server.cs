using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class Server : MonoBehaviour {
	public InputField name_input;
	public InputField password_input;



	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}

	public void OnStartClick() {
		if (name_input.text == "" || password_input.text == "") {
			Debug.Log ("no name/password entered");
			return;
		}

		if (PlayerPrefs.HasKey (name_input.text)) {
			string pass = PlayerPrefs.GetString (name_input.text);
			if (pass == password_input.text) {
				UnityEngine.SceneManagement.SceneManager.LoadScene (1);
			} else {
				Debug.Log ("Wrong password");
			}
		} else {
			Debug.Log ("new player");
			PlayerPrefs.SetString (name_input.text, password_input.text);
			UnityEngine.SceneManagement.SceneManager.LoadScene (1);

		}

	}
}
