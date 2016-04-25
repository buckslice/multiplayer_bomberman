using UnityEngine;
using System.IO;

// packet types
// byte put at beginning of packet indicating what type of message it is
// can use this to determine order to read back data in
public enum PacketType : byte {
    LOGIN,
    MESSAGE,
    TRANSFORM_SYNC,
    SCORE_UPDATE,
    // more to be added in future

}

public class Packet {

    private byte[] buffer;

    private MemoryStream stream;

    private BinaryWriter writer;
    private BinaryReader reader;
    /// <summary>
    /// Build a packet for writing data with default buffer size of 1024 bytes
    /// </summary>
    public Packet(PacketType type) {
        buffer = new byte[1024];
        stream = new MemoryStream(buffer);
        writer = new BinaryWriter(stream);
        Write((byte)type);
    }
    /// <summary>
    /// Build a packet for writing data with specified buffer size in bytes
    /// </summary>
    public Packet(PacketType type, int bufsize) {
        buffer = new byte[bufsize];
        stream = new MemoryStream(buffer);
        writer = new BinaryWriter(stream);
        Write((byte)type);
    }
    /// <summary>
    /// Build a packet for reading data
    /// </summary>
    /// <param name="buffer">data to be read</param>
    public Packet(byte[] buffer) {
        stream = new MemoryStream(buffer);
        reader = new BinaryReader(stream);
    }
    /// <summary>
    /// Build a copy of a packet
    /// </summary>
    /// <param name="p">packet to be copied</param>
    public Packet(Packet p) {
        int pSize = p.getSize();
        buffer = new byte[pSize];
        for (int i = 0; i < pSize; ++i)
            buffer[i] = p.buffer[i];
        stream = new MemoryStream(buffer);
        reader = new BinaryReader(stream);
    }

    // add more methods here as needed (wish we had templates lol)
    public void Write(string s) {
        writer.Write(s);
    }
    public void Write(byte b) {
        writer.Write(b);
    }
    public void Write(bool b) {
        writer.Write(b);
    }
    public void Write(int i) {
        writer.Write(i);
    }
    public void Write(float f) {
        writer.Write(f);
    }
    public void Write(Vector3 v) {
        writer.Write(v.x);
        writer.Write(v.y);
        writer.Write(v.z);
    }
    public void Write(Quaternion q) {
        writer.Write(q.x);
        writer.Write(q.y);
        writer.Write(q.z);
        writer.Write(q.w);
    }

    public string ReadString() {
        return reader.ReadString();
    }
    public byte ReadByte() {
        return reader.ReadByte();
    }
    public bool ReadBool() {
        return reader.ReadBoolean();
    }
    public int ReadInt() {
        return reader.ReadInt32();
    }
    public float ReadFloat() {
        return reader.ReadSingle();
    }
    public Vector3 ReadVector3() {
        return new Vector3(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle());
    }
    public Quaternion ReadQuaternion() {
        return new Quaternion(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle());
    }

    /// <summary>
    /// Returns packet data to be sent over network
    /// </summary>
    /// <returns></returns>
    public byte[] getData() {
        return buffer;
    }
    /// <summary>
    /// Gets length of data
    /// </summary>
    /// <returns></returns>
    public int getSize() {
        return (int)stream.Position;
    }

}
