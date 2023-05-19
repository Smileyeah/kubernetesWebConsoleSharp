using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using k8s;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;

namespace SignalR.Hubs;

public class KubernetesHub : Hub
{
    private readonly IKubernetes _kubernetesClient;
    
    public KubernetesHub(IKubernetes kubernetesClient)
    {
        this._kubernetesClient = kubernetesClient;
    }

    public async Task ConnectKubeAsync(string workload)
    {
        Console.WriteLine(this.Context.ConnectionId);
        var pods = await _kubernetesClient.ListNamespacedPodAsync("default", null, null, null, $"qcloud-app={workload}");
        var pod = pods.Items.First();
        
        var webSocket =
            await _kubernetesClient.WebSocketNamespacedPodExecAsync(
                pod.Metadata.Name,
                "default",
                new[] { "/bin/bash" },
                pod.Spec.Containers[0].Name).ConfigureAwait(false);
        
        this.Context.Items.Add("websocket", webSocket);
        var streamDemuxer = new StreamDemuxer(webSocket);
        var stdoutStream = streamDemuxer.GetStream(ChannelIndex.StdOut, null);
        var stdinStream = streamDemuxer.GetStream(null, ChannelIndex.StdIn);
        var resizeStream = streamDemuxer.GetStream(null, ChannelIndex.Resize);
        await resizeStream.WriteAsync(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(new
        {
            Width = short.MaxValue,
            Height = short.MaxValue
        })));
        streamDemuxer.Start();
        
        this.Context.Items.Add("streamDemuxer", streamDemuxer);
        this.Context.Items.Add("stdoutStream", stdoutStream);
        this.Context.Items.Add("stdinStream", stdinStream);
    }

    public async Task KubeReadTabAsync(CancellationToken cancellationToken)
    {
        var internalStream = this.Context.Items["stdinStream"] as MuxedStream;
        if (internalStream == null)
        {
            return;
        }
        
        // do something with the stream item
        await internalStream.WriteAsync(Encoding.ASCII.GetBytes("\r"));
    }

    public async Task Send(ChannelReader<string> stream)
    {
        var internalStream = this.Context.Items["stdinStream"] as MuxedStream;
        if (internalStream == null)
        {
            return;
        }
        
        while (await stream.WaitToReadAsync())
        {
            while (stream.TryRead(out var item))
            {
                // do something with the stream item
                await internalStream.WriteAsync(Encoding.ASCII.GetBytes(item));
            }
        }
    }
    
    public ChannelReader<string> Receive(
        CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<string>();

        // We don't want to await WriteItemsAsync, otherwise we'd end up waiting 
        // for all the items to be written before returning the channel back to
        // the client.
        _ = WriteItemsAsync(channel.Writer, cancellationToken);

        return channel.Reader;
    }

    private async Task WriteItemsAsync(
        ChannelWriter<string> writer,
        CancellationToken cancellationToken)
    {
        Exception localException = null;
        try
        {
            var stream = this.Context.Items["stdoutStream"] as MuxedStream;
            if (stream == null)
            {
                return;
            }

            using var stdout = new StreamReader(stream, Encoding.ASCII);

            while (await stdout.ReadLineAsync(cancellationToken) is { } line)
            {
                if (line.Contains("[?2004h"))// 去除不知名字符
                {
                    line = line.Substring("[?2004h".Length);
                }

                if (line.Contains("[K"))// 去除不知名字符
                {
                    line = line.Substring("[K".Length);
                }

                if (line.Contains("[?2004l")) // 去除不知名字符
                {
                    continue;
                }
                
                await writer.WriteAsync(line, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            localException = ex;
        }
        finally
        {
            writer.Complete(localException);
        }
    }
}