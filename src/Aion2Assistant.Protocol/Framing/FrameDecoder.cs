using System.Buffers;
using System.Buffers.Binary;
using Aion2Assistant.Protocol.Wire;
using K4os.Compression.LZ4;

namespace Aion2Assistant.Protocol.Framing;

public delegate void FrameHandler(ushort opcode, ReadOnlySpan<byte> body);

/// <summary>
/// Découpe un flux TCP réassemblé en trames et les transmet au gestionnaire. Après une perte de données
/// (ou un démarrage en cours de flux), il cherche le prochain heartbeat plutôt que de deviner une limite :
/// deviner produirait des dégâts fantômes.
/// </summary>
public sealed class FrameDecoder
{
    public const int MaxFrameSize = 65_535;
    public const int MaxUnbundledWait = 16_384;
    public const int LengthOverhead = 4;
    private const int MinLength = 6;
    private const int MaxHuntDrop = 256 * 1024;
    private const int MaxBundleSize = 8 * 1024 * 1024;
    private const int MaxDepth = 4;

    private readonly FrameHandler _handler;
    private byte[] _buffer = new byte[64 * 1024];
    private int _start;
    private int _end;
    private bool _hunting;
    private long _huntDropped;

    public FrameDecoder(FrameHandler handler, bool startsAligned = true)
    {
        _handler = handler;
        _hunting = !startsAligned;
    }

    public FrameStats Stats { get; } = new();

    public void Feed(ReadOnlySpan<byte> data)
    {
        Append(data);
        Drain();
    }

    /// <summary>Appelé après un trou dans le flux : jette le tampon et chasse le prochain heartbeat.</summary>
    public void Reset()
    {
        _start = _end = 0;
        _hunting = true;
        _huntDropped = 0;
    }

    private void Drain()
    {
        while (_end > _start)
        {
            var span = _buffer.AsSpan(_start, _end - _start);

            if (_hunting)
            {
                var at = FindHeartbeat(span);
                if (at < 0)
                {
                    var drop = Math.Max(0, span.Length - 2);
                    Stats.DroppedBytes += drop;
                    _huntDropped += drop;
                    _start += drop;
                    if (_huntDropped > MaxHuntDrop) { _hunting = false; _huntDropped = 0; }
                    return;
                }
                Stats.DroppedBytes += at;
                _start += at;
                _hunting = false;
                _huntDropped = 0;
                continue;
            }

            if (span[0] == 0x00) { _start++; continue; }

            if (!Varint.TryRead(span, 0, out var len, out var vlen))
            {
                if (span.Length < Varint.MaxBytes) return;
                Resync();
                continue;
            }

            var size = (long)len + vlen - LengthOverhead;
            if (len < MinLength || size > MaxFrameSize)
            {
                Resync();
                continue;
            }

            if (size > span.Length)
            {
                // IsBundle == null : en-tête incomplet, on attend plus de données au lieu de resynchroniser.
                if (size > MaxUnbundledWait && IsBundle(span, vlen) == false) { Resync(); continue; }
                return;
            }

            HandleFrame(span[..(int)size], vlen, 0);
            _start += (int)size;
        }
    }

    private void Resync()
    {
        Stats.Resyncs++;
        Stats.DroppedBytes++;
        _start++;
        _hunting = true;
    }

    private void HandleFrame(ReadOnlySpan<byte> frame, int vlen, int depth)
    {
        var o = vlen;
        if (o < frame.Length && frame[o] is >= 0xF0 and <= 0xFE) o++;
        if (frame.Length - o < 2) return;

        if (frame[o] == 0xFF && frame[o + 1] == 0xFF)
        {
            HandleBundle(frame[(o + 2)..], depth); // ses trames internes seront comptées une à une
            return;
        }

        Stats.Frames++;
        var opcode = (ushort)(frame[o] << 8 | frame[o + 1]);
        _handler(opcode, frame[(o + 2)..]);
    }

    private void HandleBundle(ReadOnlySpan<byte> content, int depth)
    {
        if (depth >= MaxDepth || content.Length < 4) { Stats.BadBundles++; return; }
        var rawSize = BinaryPrimitives.ReadUInt32LittleEndian(content);
        if (rawSize is 0 or > MaxBundleSize) { Stats.BadBundles++; return; }

        var target = ArrayPool<byte>.Shared.Rent((int)rawSize);
        try
        {
            var decoded = LZ4Codec.Decode(content[4..], target.AsSpan(0, (int)rawSize));
            if (decoded != (int)rawSize) { Stats.BadBundles++; return; }
            Stats.Bundles++;
            WalkInner(target.AsSpan(0, decoded), depth + 1);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(target);
        }
    }

    /// <summary>Trames d'un bundle décompressé : pas de resynchronisation, la première longueur invalide termine le bundle.</summary>
    private void WalkInner(ReadOnlySpan<byte> data, int depth)
    {
        var pos = 0;
        while (pos < data.Length)
        {
            if (data[pos] == 0x00) { pos++; continue; }
            if (!Varint.TryRead(data, pos, out var len, out var vlen)) return;
            var size = (long)len + vlen - LengthOverhead;
            if (len < MinLength || size > data.Length - pos) return;
            HandleFrame(data.Slice(pos, (int)size), vlen, depth);
            pos += (int)size;
        }
    }

    /// <summary>true/false si l'en-tête permet de décider, null s'il manque des octets.</summary>
    private static bool? IsBundle(ReadOnlySpan<byte> span, int vlen)
    {
        var o = vlen;
        if (span.Length <= o) return null;
        if (span[o] is >= 0xF0 and <= 0xFE) o++;
        if (span.Length < o + 2) return null;
        return span[o] == 0xFF && span[o + 1] == 0xFF;
    }

    private static int FindHeartbeat(ReadOnlySpan<byte> span)
    {
        for (var i = 0; i + 2 < span.Length; i++)
        {
            if ((span[i] == 0x0E || span[i] == 0x06) && span[i + 1] == 0x00 && span[i + 2] == 0x36) return i;
        }
        return -1;
    }

    private void Append(ReadOnlySpan<byte> data)
    {
        if (_start > 0 && _end + data.Length > _buffer.Length)
        {
            Buffer.BlockCopy(_buffer, _start, _buffer, 0, _end - _start);
            _end -= _start;
            _start = 0;
        }
        if (_end + data.Length > _buffer.Length)
        {
            var bigger = new byte[Math.Max(_buffer.Length * 2, _end + data.Length)];
            Buffer.BlockCopy(_buffer, 0, bigger, 0, _end);
            _buffer = bigger;
        }
        data.CopyTo(_buffer.AsSpan(_end));
        _end += data.Length;
    }
}
