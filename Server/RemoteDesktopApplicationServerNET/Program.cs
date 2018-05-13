using Microsoft.AspNet.SignalR;
using Microsoft.Owin.Cors;
using Microsoft.Owin.Hosting;
using Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RemoteDesktopApplicationServerNET
{
    internal enum LogType
    {
        Done = ConsoleColor.Green,
        Heads = ConsoleColor.Blue,
        Warning = ConsoleColor.Yellow,
        Error = ConsoleColor.Red
    }
    static class Utils
    {
        public static void Log(LogType color, string message)
        {
            Console.ForegroundColor = (ConsoleColor)color;
            Console.WriteLine(message);
        }
    } 
    internal class Program
    {
        private static void Main()
        {
            string url = "http://172.20.10.2:8080";
            using (WebApp.Start(url))
            {
                Utils.Log(LogType.Heads, $"Server running on : {url}");
                Console.ReadLine();
            }
        } 
    } 
    internal class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseCors(CorsOptions.AllowAll);
            app.MapSignalR();
        }
    }
    public class BroadCastHub : Hub
    {
        public static List<string> OnlineList = new List<string>();
        public static List<Connection> Connections = new List<Connection>();

        public override Task OnConnected()
        {
            OnlineList.Add(Context.ConnectionId);
            Utils.Log(LogType.Done, $"CONNECTED : {Context.ConnectionId}");
            return base.OnConnected();
        }
        public override Task OnDisconnected(bool stopCalled)
        {
            try
            {
                OnlineList.Remove(OnlineList.FirstOrDefault(x => x == Context.ConnectionId));
                Utils.Log(LogType.Error, $"DISCONNECTED : {Context.ConnectionId}");
                if (Connections.Count(x => x.SenderId == Context.ConnectionId) != 0)
                {
                    Parallel.ForEach(Connections.FirstOrDefault(x => x.SenderId == Context.ConnectionId)?.ReceiversId ?? throw new InvalidOperationException(), id =>
                    {
                        Clients.Client(id).StartBroadCast(false);
                    });
                    Utils.Log(LogType.Error, $"\tDISCONNECTED SENDER");
                }
            }
            catch (Exception ex)
            {
                Utils.Log(LogType.Error, $"ERROR (DISCONNECT) : {ex.Message}");
            }
            return base.OnDisconnected(stopCalled);
        }
        public override Task OnReconnected()
        {
            OnlineList.Add(Context.ConnectionId);
            Utils.Log(LogType.Done, $"RE-CONNECTED : {Context.ConnectionId}");
            return base.OnReconnected();
        }
        public void StopBroadCastReceiving(string targetId)
        {
            try
            {
                Utils.Log(LogType.Heads, "++REQUEST : StopBroadCastReceiving");
                var selectedItem = Connections.FirstOrDefault(x => x.SenderId == targetId);
                if (selectedItem != null)
                {
                    selectedItem.ReceiversId.RemoveAll(x => x == Context.ConnectionId);
                    if (selectedItem.ReceiversId.Count == 0)
                    {
                        Clients.Client(targetId).StartBroadCast(false);
                        Connections.Remove(Connections.FirstOrDefault(x => x.SenderId == targetId));
                    }
                }

                Utils.Log(LogType.Error, "\tStopped BroadCast");
            }
            catch (Exception ex)
            {
                Utils.Log(LogType.Warning, $"\tERROR : {ex.Message}");
            }
        }
        public void ConnectionStart(string targetId)
        {
            ReturnResult result = new ReturnResult();
            try
            {
                if (targetId == Context.ConnectionId)
                    CastResault(ref result, false, "You cannot share your own screen with yourself!");
                else
                {
                    Utils.Log(LogType.Heads, $"++RECEIVED received parameter 'TargetID' ({targetId})");
                    if (OnlineList.Count(x => x == targetId) == 0)
                        CastResault(ref result, false, "Target Not Found!");
                    else
                    {
                        Connection newConn;
                        if (Connections.Count(x => x.SenderId == targetId) != 0) // current connection 
                            newConn = Connections.FirstOrDefault(x => x.SenderId == targetId);
                        else
                        {
                            newConn = new Connection {SenderId = targetId};
                            Connections.Add(newConn);
                            Clients.Client(targetId).StartBroadCast(true);
                        }
                        if (newConn != null && newConn.ReceiversId.Count(x => x == Context.ConnectionId) == 0)
                            newConn.ReceiversId.Add(Context.ConnectionId);
                        CastResault(ref result, true);
                    }
                }
            }
            catch (Exception ex)
            {
                CastResault(ref result, false, ex.Message);
            }
            finally
            {
                Clients.Client(Context.ConnectionId).EstablishConnection(result);
                string at = result.Result == false ? $" \n\t'Detail':{result.Detail}" : "";
                Utils.Log(LogType.Done, $"--SENT resault parameters 'State': {result.Result}" + at);
            }
        }

        public void SendFile(byte[] parameters, string targetId,int packageSize,int totalBytesRead, string extension) // only one way transfer
        {
            if (OnlineList.Count(x => x == targetId) == 0)
            {
                Utils.Log(LogType.Error,$"--SEND FILE ERROR targetID:{targetId}");
                return;
            }
            Utils.Log(LogType.Done,$"--SENDFILE METHOD : File Sent , targetID : {targetId}");
            Clients.Client(targetId).ReceiveFile(parameters,packageSize, totalBytesRead,extension);
        }
        public void SendFrames(byte[] jFrame)
        { 
            Utils.Log(LogType.Heads, $"++REQUEST : SendFrames ({Context.ConnectionId}");
            var connection = Connections.FirstOrDefault(x => x.SenderId == Context.ConnectionId);
            if (connection == null)
                return;
            if (connection.ReceiversId.Count == 0) // if receivers count == 0 then stopBroadCast
                Connections.Remove(Connections.FirstOrDefault(x => x.SenderId == Context.ConnectionId));

            try
            {
                Parallel.ForEach(connection.ReceiversId, id =>
                {
                    Clients.Client(id).ReceiveFrame(jFrame);
                });
                Utils.Log(LogType.Done, "--SENT Frame ..");
            }
            catch (Exception ex)
            {
                Utils.Log(LogType.Warning, $"\t SendERROR : {ex.Message}");
            }
        }
        public void CastResault(ref ReturnResult ret, bool state, string detail = "")
        {
            ret.Detail = detail;
            ret.Result = state;
        }
    }
    public class ReturnResult
    {
        public bool Result { get; set; }
        public string Detail { get; set; }
    }
    public class Connection
    {
        public string SenderId { get; set; }
        public List<string> ReceiversId = new List<string>();
    }
}
