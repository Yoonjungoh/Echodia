using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;

namespace ServerCore
{
    public abstract class PacketSession : Session
    {
        public const int HeaderSize = sizeof(ushort) * 2;

        public sealed override int OnRecv(ArraySegment<byte> buffer)
        {
            int consumed = 0;
            while (buffer.Count - consumed >= HeaderSize)
            {
                int offset = buffer.Offset + consumed;
                int length = BitConverter.ToUInt16(buffer.Array, offset);
                if (length < HeaderSize)
                    throw new InvalidDataException("Invalid game frame length.");
                if (length > buffer.Count - consumed)
                    break;
                OnRecvPacket(new ArraySegment<byte>(buffer.Array, offset, length));
                consumed += length;
            }
            return consumed;
        }

        public abstract void OnRecvPacket(ArraySegment<byte> buffer);
    }

    public abstract class Session
    {
        const int BatchSize = 16 * 1024;
        readonly RecvBuffer _receive = new RecvBuffer(ushort.MaxValue);
        readonly Queue<ArraySegment<byte>> _outbound = new Queue<ArraySegment<byte>>();
        SslStream _stream;
        EndPoint _remote;
        bool _writing;
        int _closed;

        public bool IsConnected => Volatile.Read(ref _stream) != null && Volatile.Read(ref _closed) == 0;
        public abstract void OnConnected(EndPoint endPoint);
        public abstract int OnRecv(ArraySegment<byte> buffer);
        public abstract void OnSend(int numOfBytes);
        public abstract void OnDisconnected(EndPoint endPoint);

        public void Start(SslStream stream, EndPoint remote)
        {
            if (!stream.IsAuthenticated || !stream.IsEncrypted)
                throw new InvalidOperationException("An authenticated TLS stream is required.");
            _remote = remote;
            Volatile.Write(ref _stream, stream);
            try
            {
                OnConnected(remote);
                _ = ReceiveAsync();
            }
            catch
            {
                Disconnect();
                throw;
            }
        }

        public void Send(List<ArraySegment<byte>> buffers)
        {
            lock (_outbound)
            {
                if (!IsConnected || buffers.Count == 0)
                    return;
                foreach (var buffer in buffers)
                    _outbound.Enqueue(buffer);
                if (_writing)
                    return;
                _writing = true;
            }
            _ = WriteAsync();
        }

        public void Send(ArraySegment<byte> buffer)
        {
            lock (_outbound)
            {
                if (!IsConnected)
                    return;
                _outbound.Enqueue(buffer);
                if (_writing)
                    return;
                _writing = true;
            }
            _ = WriteAsync();
        }

        async Task WriteAsync()
        {
            byte[] batch = null;
            try
            {
                while (true)
                {
                    ArraySegment<byte> output;
                    lock (_outbound)
                    {
                        if (_outbound.Count == 0)
                        {
                            _writing = false;
                            return;
                        }
                        output = _outbound.Dequeue();
                        if (_outbound.Count > 0 && output.Count + _outbound.Peek().Count <= BatchSize)
                        {
                            if (batch == null)
                                batch = ArrayPool<byte>.Shared.Rent(BatchSize);
                            int total = 0;
                            while (true)
                            {
                                Buffer.BlockCopy(output.Array, output.Offset, batch, total, output.Count);
                                total += output.Count;
                                if (_outbound.Count == 0 || _outbound.Peek().Count > BatchSize - total)
                                    break;
                                output = _outbound.Dequeue();
                            }
                            output = new ArraySegment<byte>(batch, 0, total);
                        }
                    }
                    await _stream.WriteAsync(output.Array, output.Offset, output.Count).ConfigureAwait(false);
                    OnSend(output.Count);
                }
            }
            catch (Exception error)
            {
                ReportFailure(error);
                Disconnect();
            }
            finally
            {
                if (batch != null)
                    ArrayPool<byte>.Shared.Return(batch, clearArray: true);
            }
        }

        async Task ReceiveAsync()
        {
            try
            {
                while (IsConnected)
                {
                    if (_receive.FreeSize == 0)
                        _receive.Clean();
                    var free = _receive.WriteSegment;
                    if (free.Count == 0)
                        throw new InvalidDataException("Game receive buffer exhausted.");
                    int count = await _stream.ReadAsync(free.Array, free.Offset, free.Count).ConfigureAwait(false);
                    if (count == 0)
                        break;
                    _receive.OnWrite(count);
                    int consumed = OnRecv(_receive.ReadSegment);
                    if (consumed < 0 || !_receive.OnRead(consumed))
                        throw new InvalidDataException("Invalid consumed length.");
                }
            }
            catch (Exception error)
            {
                ReportFailure(error);
            }
            finally
            {
                Disconnect();
            }
        }

        void ReportFailure(Exception error)
        {
            if (Volatile.Read(ref _closed) == 0)
                Console.Error.WriteLine("Game session closed: " + error.GetType().Name);
        }

        public void Disconnect()
        {
            if (_stream == null || Interlocked.Exchange(ref _closed, 1) != 0)
                return;
            try
            {
                _stream.Dispose();
            }
            finally
            {
                lock (_outbound)
                    _outbound.Clear();
                OnDisconnected(_remote);
            }
        }
    }
}
