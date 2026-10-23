using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Schreibt eine Netzwerk-Nachricht Byte fuer Byte. Little Endian, ohne
/// Reflection und ohne Allokation pro Wert - im Lauf gehen davon pro Sekunde
/// einige hundert raus.
///
/// Zahlen, die meist klein sind (Ids, Zaehler), gehen als VarInt: 1 Byte bis
/// 127, 2 bis 16383. Positionen relativ zu einem Ursprung als 16-Bit-Festkomma
/// (<see cref="PosUnit"/>), das reicht +-512 Einheiten um den Spieler herum.
/// </summary>
public sealed class NetWriter
{
    /// <summary>Kleinster Schritt einer kompakten Position (1/64 Einheit = halber Bildpunkt bei PPU 32).</summary>
    public const float PosUnit = 1f / 64f;

    private byte[] data;
    private int length;

    public NetWriter(int capacity = 256)
    {
        data = new byte[Mathf.Max(16, capacity)];
    }

    public int Length => length;
    public byte[] Data => data;

    public void Clear() => length = 0;

    /// <summary>Kopie der geschriebenen Bytes (fuer Transporte, die selbst puffern).</summary>
    public byte[] ToArray()
    {
        var copy = new byte[length];
        Buffer.BlockCopy(data, 0, copy, 0, length);
        return copy;
    }

    private void Ensure(int more)
    {
        if (length + more <= data.Length) return;
        int size = data.Length * 2;
        while (size < length + more) size *= 2;
        Array.Resize(ref data, size);
    }

    public void Byte(byte v)
    {
        Ensure(1);
        data[length++] = v;
    }

    public void Bool(bool v) => Byte(v ? (byte)1 : (byte)0);

    public void Short(short v)
    {
        Ensure(2);
        data[length++] = (byte)v;
        data[length++] = (byte)(v >> 8);
    }

    public void UShort(ushort v) => Short((short)v);

    public void Int(int v)
    {
        Ensure(4);
        data[length++] = (byte)v;
        data[length++] = (byte)(v >> 8);
        data[length++] = (byte)(v >> 16);
        data[length++] = (byte)(v >> 24);
    }

    public void Long(long v)
    {
        Int((int)v);
        Int((int)(v >> 32));
    }

    public void ULong(ulong v) => Long((long)v);

    public void Float(float v) => Int(BitConverter.SingleToInt32Bits(v));

    public void Double(double v) => Long(BitConverter.DoubleToInt64Bits(v));

    /// <summary>Nicht-negative Zahl in 7-Bit-Gruppen.</summary>
    public void VarUInt(uint v)
    {
        while (v >= 0x80)
        {
            Byte((byte)(v | 0x80));
            v >>= 7;
        }
        Byte((byte)v);
    }

    /// <summary>Ganzzahl mit Vorzeichen (ZigZag), kleine Betraege bleiben kurz.</summary>
    public void VarInt(int v) => VarUInt((uint)((v << 1) ^ (v >> 31)));

    public void String(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            VarUInt(0);
            return;
        }

        int count = Encoding.UTF8.GetByteCount(s);
        VarUInt((uint)count);
        Ensure(count);
        Encoding.UTF8.GetBytes(s, 0, s.Length, data, length);
        length += count;
    }

    public void Vector2(Vector2 v)
    {
        Float(v.x);
        Float(v.y);
    }

    /// <summary>Position relativ zu <paramref name="origin"/>, 16 Bit je Achse (siehe <see cref="PosUnit"/>).</summary>
    public void CompactPos(Vector2 v, Vector2 origin)
    {
        Short(ToShort((v.x - origin.x) / PosUnit));
        Short(ToShort((v.y - origin.y) / PosUnit));
    }

    /// <summary>Winkel in Grad als ein Byte (1,4 Grad Schritt).</summary>
    public void Angle(float degrees)
    {
        float a = Mathf.Repeat(degrees, 360f);
        Byte((byte)Mathf.RoundToInt(a / 360f * 256f));
    }

    /// <summary>Faktor 0..~16 in 1/16 Schritten - fuer Skalierungen.</summary>
    public void Scale(float v)
    {
        Short(ToShort(v * 256f));
    }

    public void Color32(Color32 c)
    {
        Ensure(4);
        data[length++] = c.r;
        data[length++] = c.g;
        data[length++] = c.b;
        data[length++] = c.a;
    }

    public void Bytes(byte[] src, int offset, int count)
    {
        Ensure(count);
        Buffer.BlockCopy(src, offset, data, length, count);
        length += count;
    }

    /// <summary>Platz fuer einen Wert, der erst spaeter feststeht (z.B. eine Anzahl).</summary>
    public int ReserveUShort()
    {
        int at = length;
        Short(0);
        return at;
    }

    public void PatchUShort(int at, ushort v)
    {
        data[at] = (byte)v;
        data[at + 1] = (byte)(v >> 8);
    }

    private static short ToShort(float v)
    {
        int i = Mathf.RoundToInt(v);
        if (i > short.MaxValue) return short.MaxValue;
        if (i < short.MinValue) return short.MinValue;
        return (short)i;
    }
}

/// <summary>Liest, was <see cref="NetWriter"/> geschrieben hat. Liest nie ueber das Ende hinaus.</summary>
public sealed class NetReader
{
    private byte[] data;
    private int position, end;

    public NetReader() { }

    public NetReader(byte[] bytes, int offset, int count)
    {
        Reset(bytes, offset, count);
    }

    public void Reset(byte[] bytes, int offset, int count)
    {
        data = bytes;
        position = offset;
        end = offset + count;
    }

    public int Remaining => end - position;

    /// <summary>Kaputte oder abgeschnittene Nachricht - der Empfaenger verwirft sie.</summary>
    public bool Failed { get; private set; }

    private bool Has(int n)
    {
        if (position + n <= end) return true;
        Failed = true;
        position = end;
        return false;
    }

    public byte Byte() => Has(1) ? data[position++] : (byte)0;

    public bool Bool() => Byte() != 0;

    public short Short()
    {
        if (!Has(2)) return 0;
        short v = (short)(data[position] | (data[position + 1] << 8));
        position += 2;
        return v;
    }

    public ushort UShort() => (ushort)Short();

    public int Int()
    {
        if (!Has(4)) return 0;
        int v = data[position] | (data[position + 1] << 8) | (data[position + 2] << 16) | (data[position + 3] << 24);
        position += 4;
        return v;
    }

    public long Long()
    {
        uint lo = (uint)Int();
        long hi = Int();
        return (hi << 32) | lo;
    }

    public ulong ULong() => (ulong)Long();

    public float Float() => BitConverter.Int32BitsToSingle(Int());

    public double Double() => BitConverter.Int64BitsToDouble(Long());

    public uint VarUInt()
    {
        uint result = 0;
        int shift = 0;
        while (shift < 35)
        {
            byte b = Byte();
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0 || Failed) return result;
            shift += 7;
        }
        Failed = true;
        return result;
    }

    public int VarInt()
    {
        uint v = VarUInt();
        return (int)(v >> 1) ^ -(int)(v & 1);
    }

    public string String()
    {
        int count = (int)VarUInt();
        if (count == 0) return "";
        if (!Has(count)) return "";
        string s = Encoding.UTF8.GetString(data, position, count);
        position += count;
        return s;
    }

    public Vector2 Vector2() => new Vector2(Float(), Float());

    public Vector2 CompactPos(Vector2 origin)
    {
        float x = Short() * NetWriter.PosUnit;
        float y = Short() * NetWriter.PosUnit;
        return new Vector2(origin.x + x, origin.y + y);
    }

    public float Angle() => Byte() / 256f * 360f;

    public float Scale() => Short() / 256f;

    public Color32 Color32()
    {
        if (!Has(4)) return new Color32(255, 255, 255, 255);
        var c = new Color32(data[position], data[position + 1], data[position + 2], data[position + 3]);
        position += 4;
        return c;
    }

    public void Skip(int count)
    {
        if (Has(count)) position += count;
    }
}
