using UnityEngine;
using System.Collections;

// attach this script to objects you only want
// one of and wont be destroyed on scene change
public class SingletonTraveler : MonoBehaviour {

    public static bool created = false;

    void Awake() {
        if (created) {
            Destroy(gameObject);
        } else {
            created = true;
            DontDestroyOnLoad(gameObject);
        }
    }
}
