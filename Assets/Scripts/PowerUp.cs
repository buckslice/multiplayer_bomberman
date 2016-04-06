using UnityEngine;
using System.Collections;

public class PowerUp : MonoBehaviour {

	void OnTriggerEnter(Collider col)
    {
        if (col.tag == "Player")
        {
            Destroy(gameObject);
        }
    }
}
