using System.Buffers;
using System.Net.WebSockets;
using k8s;

namespace SignalR;

public static class WebSocketEcho
{
    private const int MAXFRAMESIZE = 15 * 1024 * 1024; // 15MB
    public static async Task Echo(IKubernetes kubernetes, WebSocket webSocket, string workload,
        CancellationToken cancellationToken)
    {
        var pods = await kubernetes.ListNamespacedPodAsync("default", null, null, null, $"qcloud-app={workload}",
            cancellationToken: cancellationToken);
        var pod = pods.Items.First();

        var kubeWebSocket =
            await kubernetes.WebSocketNamespacedPodExecAsync(
                pod.Metadata.Name,
                "default",
                new[] { "/bin/bash" },
                pod.Spec.Containers[0].Name, cancellationToken: cancellationToken);

        Task.Run(async () =>
        {
            await WebsocketReadLoop(kubeWebSocket, webSocket, cancellationToken);
        });

        var buffer = new byte[1024 * 4];
        var receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!receiveResult.CloseStatus.HasValue)
        {
            var count = receiveResult.Count;
            var writeBuffer = ArrayPool<byte>.Shared.Rent(Math.Min(count, MAXFRAMESIZE) + 1);

            try
            {
                writeBuffer[0] = (byte)ChannelIndex.StdIn;
                for (var i = 0; i < count; i += MAXFRAMESIZE)
                {
                    var c = Math.Min(count - i, MAXFRAMESIZE);
                    Buffer.BlockCopy(buffer, i, writeBuffer, 1, c);
                    var segment = new ArraySegment<byte>(writeBuffer, 0, c + 1);
                    await kubeWebSocket.SendAsync(segment, WebSocketMessageType.Binary, false, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(writeBuffer);
            }

            receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        await webSocket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            CancellationToken.None);
    }

    private static async Task WebsocketReadLoop(WebSocket kubeWebSocket, WebSocket webSocket,
        CancellationToken cancellationToken)
    {
        // Get a 1KB buffer
        var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);

        try
        {
            var segment = new ArraySegment<byte>(buffer);

            while (!cancellationToken.IsCancellationRequested && kubeWebSocket.CloseStatus == null)
            {
                // We always get data in this format:
                // [stream index] (1 for stdout, 2 for stderr)
                // [payload]
                var result = await kubeWebSocket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);

                // Ignore empty messages
                if (result.Count < 2)
                {
                    continue;
                }

                var extraByteCount = 1;

                while (true)
                {
                    var bytesCount = result.Count - extraByteCount;

                    var arraySegment = new ArraySegment<byte>(buffer, extraByteCount, bytesCount);
                    await webSocket.SendAsync(arraySegment,
                        WebSocketMessageType.Text,
                        result.EndOfMessage,
                        cancellationToken);

                    if (result.EndOfMessage)
                    {
                        break;
                    }

                    extraByteCount = 0;
                    result = await kubeWebSocket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}