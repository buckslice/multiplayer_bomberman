using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class RoomPanelEditor : MonoBehaviour {

    public Text roomNameText;
    public Text playerCountText;
    public Button joinRoomButton;

    public void updateText(string roomName, int playerCountInRoom) {
        roomNameText.text = roomName;
        playerCountText.text = playerCountInRoom + playerCountInRoom > 1 ? " Players" : " Player";
    }
}
