using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.Collections;

public class LobbyUIController : MonoBehaviour {

    // lobby screen stuff
    public Text playerNamesText;
    public Text chatLogText;
    public InputField chatInputBar;

    public GameClient client {private get; set;}

    private bool typingInChat = false;
    private bool firstChat = true;

    // Use this for initialization
    void Start () {
        chatInputBar.gameObject.SetActive(false);
	}
	
	// Update is called once per frame
	void Update () {
        // do chat update
        if (Input.GetKeyDown(KeyCode.Return)) {
            typingInChat = !typingInChat;
            chatInputBar.gameObject.SetActive(typingInChat);
            if (typingInChat) {
                chatInputBar.ActivateInputField();
            } else if (chatInputBar.text.Length != 0) {   // send message if just hit enter
                client.sendChatMessage(chatInputBar.text);
                chatInputBar.text = "";
            }
        }
    }

    public void setPlayerNames(string names) {
        playerNamesText.text = names;
    }

    public void processChatString(string name, Color32 c, string content) {
        if (firstChat) {
            chatLogText.text = "";
            chatLogText.rectTransform.sizeDelta = new Vector2(0, 0);
            firstChat = false;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append(getNameWithColor(name, c));
        sb.Append(": ");
        sb.Append(content);
        sb.Append("\n");
        sb.Append(chatLogText.text);

        chatLogText.text = sb.ToString();
        float newHeight = LayoutUtility.GetPreferredHeight(chatLogText.rectTransform);
        chatLogText.rectTransform.sizeDelta = new Vector2(0, newHeight);
    }

    public string getNameWithColor(string name, Color32 color) {
        StringBuilder sb = new StringBuilder();
        sb.Append("<color=#");
        sb.Append(color.r.ToString("X2"));
        sb.Append(color.g.ToString("X2"));
        sb.Append(color.b.ToString("X2"));
        sb.Append(">");
        sb.Append(name);
        sb.Append("</color>");
        return sb.ToString();
    }
}
