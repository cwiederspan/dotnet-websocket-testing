using System.Text;
using System.Net.WebSockets;

using Microsoft.AspNetCore.Mvc;

namespace MyWsServer.Controllers;

[ApiController]
[Route("[controller]")]
public class WebSocketController : ControllerBase {

    private readonly ILogger<WebSocketController> Logger;

    public WebSocketController(ILogger<WebSocketController> logger) {
        this.Logger = logger;
    }

    [HttpGet("/ws")]
    public async Task Get() {

        // Is this a websocket request...?
        if (this.HttpContext.WebSockets.IsWebSocketRequest == true) {

            var id = Guid.NewGuid();

            // If so, setup the webSocket variable to interact with it
            using (var webSocket = await this.HttpContext.WebSockets.AcceptWebSocketAsync()) {

                var cts = new CancellationTokenSource();

                var monitorTask = this.MonitorWebSocket(webSocket, cts);

                var randomDataTask = this.SendRandomData(webSocket, id, cts.Token);

                Task.WaitAll(monitorTask, randomDataTask);

                // Close down the socket and end
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "The WebSocket connection has been closed",
                    CancellationToken.None
                );
            }
        }
        else {
            this.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

    private async Task MonitorWebSocket(WebSocket webSocket, CancellationTokenSource cts) {

        var buffer = new byte[1024 * 4];
        var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        // Start an indefinite loop to listen for incoming messages
        while (receiveResult.CloseStatus.HasValue == false && cts.Token.IsCancellationRequested == false) {

            // Echo back the incoming data back to the sender
            // await webSocket.SendAsync(
            //     new ArraySegment<byte>(buffer, 0, receiveResult.Count),
            //     receiveResult.MessageType,
            //     receiveResult.EndOfMessage,
            //     CancellationToken.None);

            this.Logger.LogDebug("Received data from client...");

            // And then wait for the next incoming message
            receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        // If the CloseStatus is set, fire off the Cancellation
        cts.Cancel();

        this.Logger.LogInformation("WebSocket connection closed by client");
    }


    private async Task SendRandomData(WebSocket webSocket, Guid id, CancellationToken cancellationToken) {

        while (cancellationToken.IsCancellationRequested == false) {

            var data = ConvertToMessage($"Testing for {id} at {DateTime.Now.ToLongTimeString()}...");
            await webSocket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);

            this.Logger.LogDebug("Sending random data to client...");

            Thread.Sleep(1000);
        }

        this.Logger.LogDebug("Random data has stopped!");
    }

    private ArraySegment<byte> ConvertToMessage(string message) {

        var bytes = Encoding.Default.GetBytes(message);
        var arraySegment = new ArraySegment<byte>(bytes);
        return arraySegment;
    }
}
