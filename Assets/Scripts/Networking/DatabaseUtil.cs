using UnityEngine;
using Mono.Data.Sqlite;
using System.Data;

public class DatabaseUtil : MonoBehaviour {

    private string conn = "URI=file:" + Application.streamingAssetsPath + "/AccountInfo.s3db";

    private IDbConnection dbconn;
    private IDbCommand cmd;
    private IDataReader reader;

    // Use this for initialization
    void Awake() {
        dbconn = new SqliteConnection(conn);
        dbconn.Open();
        cmd = dbconn.CreateCommand();

        // reset database each time game is run (for now)
        cmd.CommandText = "DROP TABLE IF EXISTS Accounts";
        cmd.ExecuteNonQuery();

        // recreate the Accounts table with name and password text fields
        cmd.CommandText = "CREATE TABLE Accounts(name TEXT NOT NULL, password TEXT NOT NULL);";
        cmd.ExecuteNonQuery();
    }

    void OnDestroy() {
        dbconn.Close();
    }

    // returns true by either having a new name or by typing in correct password for your name
    // returns false if types wrong password for existing name in database
    public bool tryLogin(string name, string password) {
        cmd.CommandText = "SELECT name,password FROM Accounts WHERE name = \"" + name + "\"";
        reader = cmd.ExecuteReader();
        string p = "";
        while (reader.Read()) {
            p = reader.GetString(1);
        }
        reader.Close();
        // if found a password at that name then return whether passwords match
        if (p != "") {
            if (password == p) {
                Debug.Log("SERVER: player login accepted");
                return true;
            } else {
                Debug.Log("SERVER: player login denied, wrong password");
                return false;
            }
        }
        // no account under this name so add to database
        cmd.CommandText = "INSERT INTO Accounts VALUES(\"" + name + "\", \"" + password + "\");";
        cmd.ExecuteNonQuery();
        Debug.Log("SERVER: new player \"" + name + "\" joined with password \"" + password + "\"");
        return true;
    }
}
