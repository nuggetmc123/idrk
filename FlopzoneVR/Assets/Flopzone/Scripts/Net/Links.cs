using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Flopzone
{
    /// <summary>A connection to "the server": the real one online, or LocalServer offline.</summary>
    public interface ILink
    {
        bool Online { get; }
        bool Connected { get; }
        void Send(Dictionary<string, object> msg);
        /// <summary>Hand over messages that arrived since last time.</summary>
        void Poll(List<Dictionary<string, object>> into);
        void Close();
        /// <summary>Non-null when this headset runs the room (and so drives the bots).</summary>
        IBotHost BotHost { get; }
    }

    /// <summary>WebSocket connection to the Flopzone Cloudflare Worker.</summary>
    public class WsLink : ILink
    {
        readonly string url;
        ClientWebSocket ws;
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> outbox = new ConcurrentQueue<string>();
        readonly SemaphoreSlim sendSignal = new SemaphoreSlim(0);
        public bool Online => true;
        public bool Connected { get; private set; }
        public bool Failed { get; private set; }
        public IBotHost BotHost => null;
        public string Error { get; private set; }

        public WsLink(string url)
        {
            this.url = url;
            Task.Run(Run);
        }

        async Task Run()
        {
            try
            {
                ws = new ClientWebSocket();
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cts.Token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(8));
                    await ws.ConnectAsync(new Uri(url), timeout.Token);
                }
                Connected = true;
                var sender = Task.Run(SendLoop);
                var buf = new byte[16384];
                var sb = new StringBuilder();
                while (ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
                {
                    var res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
                    if (res.MessageType == WebSocketMessageType.Close) break;
                    sb.Append(Encoding.UTF8.GetString(buf, 0, res.Count));
                    if (res.EndOfMessage)
                    {
                        inbox.Enqueue(sb.ToString());
                        sb.Clear();
                    }
                }
            }
            catch (Exception e)
            {
                Error = e.Message;
            }
            Connected = false;
            Failed = true;
            inbox.Enqueue("{\"t\":\"_closed\"}");
        }

        async Task SendLoop()
        {
            try
            {
                while (!cts.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    await sendSignal.WaitAsync(cts.Token);
                    while (outbox.TryDequeue(out var text))
                    {
                        var bytes = Encoding.UTF8.GetBytes(text);
                        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
                    }
                }
            }
            catch (Exception) { }
        }

        public void Send(Dictionary<string, object> msg)
        {
            if (!Connected) return;
            outbox.Enqueue(Json.Write(msg));
            sendSignal.Release();
        }

        public void Poll(List<Dictionary<string, object>> into)
        {
            while (inbox.TryDequeue(out var text))
            {
                var m = Json.ParseObject(text);
                if (m != null) into.Add(m);
            }
        }

        public void Close()
        {
            try
            {
                if (ws != null && ws.State == WebSocketState.Open)
                    ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch (Exception) { }
            cts.Cancel();
        }
    }
}
