using System;
using System.Threading;
using Communication.Proto;
using System.Net;
using Microsoft.Extensions.CommandLineUtils;

namespace Communication.Agent
{
    public class Program
    {
        private const int Interval = 100;
        private static IDClient client = new IDClient();
        private static IDServer server = new IDServer();
        private static System.Timers.Timer myTimer = new System.Timers.Timer();
        private static IPEndPoint Server;
        //private static object[] LastSpam;// = Constants.MaxMessage;
        //private static int[][] deltaSendTime;
        /*
        private static void TimeCount(object source, System.Timers.ElapsedEventArgs e) //倒计时
        {
            MessageLimit++;
        }
        */
        public static void Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.HelpOption("-h|--help");
            var server = app.Option("-s|--server", "game server endpoint", CommandOptionType.SingleValue);
            var port = app.Option("-p|--port", "agent port", CommandOptionType.SingleValue);
            var playercount = app.Option("-n|--playercount", "player count", CommandOptionType.SingleValue);
            var token = app.Option("-t|--token", "player token, leave empty to enable offline mode", CommandOptionType.SingleValue);
            var debugLevel = app.Option("-d|--debugLevel", "0 to disable debug output", CommandOptionType.SingleValue);
            var timelmt = app.Option("--timelimit", "time limit", CommandOptionType.SingleValue);
            //var messagelmt = app.Option("--msglimit", "message limit", CommandOptionType.SingleValue);
            app.OnExecute(() =>
            {
                Constants.PlayerCount = ushort.Parse(playercount.Value());
                if (Constants.PlayerCount < 1) Constants.PlayerCount = 1;
                else if (Constants.PlayerCount > 2) Constants.PlayerCount = 2;
                //LastSpam = new object[Constants.PlayerCount];
                //deltaSendTime = new int[Constants.PlayerCount][];
                //Constants.MaxMessage = int.Parse(messagelmt.Value());
                Constants.TimeLimit = double.Parse(timelmt.Value());
                for (int i = 0; i < Constants.PlayerCount; i++)
                {
                    //deltaSendTime[i] = new int[] { (int)Constants.TimeLimit + 5, (int)Constants.TimeLimit + 5 };
                }
                if (Constants.TimeLimit < 10) Constants.TimeLimit = 10;
                return MainInternal(server.Value(), ushort.Parse(port.Value()), token.Value(), int.Parse(debugLevel.Value()));
            });
            app.Execute(args);
        }

        private static int MainInternal(string ep, ushort port, string token, int debugLevel)
        {
            string[] t = ep.Split(':');
            Constants.Debug("Server endpoint: " + t);
            Server = new IPEndPoint(IPAddress.Parse(t[0]), int.Parse(t[1]));
            server.Port = port;
            Constants.Debug("Agent Listen Port: " + server.Port.ToString());
            Constants.Debug("Client Token: " + (token ?? "<offline>"));
            //for (int i = 0; i < LastSpam.Length; i++)
            //    LastSpam[i] = Environment.TickCount;

            //init timer
            myTimer.Interval = Interval;
            myTimer.Elapsed += TimedUpdate;
            myTimer.Enabled = true;
            client.OnReceive += delegate (Message message)
            {
                Console.Write("S:" + Environment.TickCount % 10000 + ",");
                server.Send(message.Content as Message); //向客户端转发Content
            };
            server.InternalQuit += delegate ()
            {
                client.Quit();
                server.Resume();
            };
            server.OnReceive += delegate (Message message)
            {
                //if (!(((message.Content as Message)?.Content as Message)?.Content is PingPacket))
                //{
                //    var now = Environment.TickCount;
                //    lock (LastSpam)
                //    {
                //        //Console.WriteLine(message.Address + " : " + now + " deltaSendTime : " + deltaSendTime[message.Address][0] + ", " + deltaSendTime[message.Address][1]);
                //        int deltaTime = now - (int)LastSpam[message.Address];
                //        if (((double)deltaTime + (double)deltaSendTime[message.Address][0] + (double)deltaSendTime[message.Address][1]) / 3.0 <= Constants.TimeLimit)
                //        {
                //            Console.WriteLine("skip client's message");
                //            return;
                //        }
                //        LastSpam[message.Address] = now;
                //        deltaSendTime[message.Address][0] = deltaSendTime[message.Address][1];
                //        deltaSendTime[message.Address][1] = deltaTime;
                //    }
                //}
                //else
                //    Constants.Debug("Ignoring PingPacket");
                Console.Write("C" + message.Address + ":" + Environment.TickCount % 10000 + ",");
                client.Send(message.Content as Message);
            };

            client.OnDisconnect += delegate ()
            {
                if (server.Count == 0)
                {
                    server.Stop(); //主动Disconnect意味着游戏结束，关闭Agent。
                    Environment.Exit(0);
                }

            };

            server.OnAccept += delegate ()
            {
                Constants.Debug($"Player Connected: {server.Count}/{Constants.PlayerCount}");
                if (server.Count == Constants.PlayerCount)
                {
                    server.Pause();
                    client.Connect(Server); //客户端满人后再向Server发送连接请求，可以省略GameStart包

                    client.Send(new Message //发送token
                    {
                        Address = -1,
                        Content = new PlayerToken
                        {
                            Token = token ?? "" //空token代表离线
                        }
                    });
                }
            };

            if (debugLevel == 0)
                Constants.Debug = new Constants.DebugFunc((str) => { });

            server.Start();

            Thread.Sleep(int.MaxValue);
            return 0;
        }

        private static void TimedUpdate(object source, System.Timers.ElapsedEventArgs e) //轮询预留
        {

        }

    }

}
