using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerPanelScript : MonoBehaviour {

    public Text playerNameText;
    public Text toggleText;
    public Toggle toggle;

    private RectTransform rt;

    // Use this for initialization
    void Awake () {
        rt = GetComponent<RectTransform>();
    }

    public void setToggle(bool b) {
        if (toggle) {
            toggle.isOn = b;
        }
    }

    public void init(Transform parent) {
        rt.SetParent(parent, false);
    }

    public void setYPos(float ypos) {
        rt.anchoredPosition = new Vector2(0, ypos);
    }

    public void setText(string playerName, string toggleText) {
        playerNameText.text = playerName;
        this.toggleText.text = toggleText;
    }

}
