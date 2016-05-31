using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class RoomPanelEditor : MonoBehaviour {
    public Text roomNameText;
    public Text playerCountText;
    public Button joinRoomButton;

    private LobbyUIController lobbyUI;
    private RectTransform rt;

    void Awake() {
        rt = GetComponent<RectTransform>();
    }

    public void init(LobbyUIController lui, Transform parent) {
        lobbyUI = lui;
        rt.SetParent(parent, false);
    }

    public void setYPos(float ypos) {
        rt.anchoredPosition = new Vector2(0, ypos);
    }

    public void setText(string roomName, string playerCountInRoom) {
        roomNameText.text = roomName;
        playerCountText.text = playerCountInRoom + (playerCountInRoom == "1" ? " Player" : " Players");
        joinRoomButton.onClick.RemoveAllListeners();
        joinRoomButton.onClick.AddListener(() => lobbyUI.tryChangeRoom(roomName));
    }
}
