using CommNet.Network;
using Smooth.Algebraics;
using System;
using System.IO;
using System.Net.Sockets;
using static KSP.UI.UITransitionBase;
using System.Runtime.Remoting.Messaging;
using UnityEngine;

namespace ksp2_data_emitter_plugin
{
    public class TestPlugin : PartModule
    {
        const String TAG = "---TESTPLUGIN---";
        static TcpListener server = null; // the server, use static to avoid multiple servers
        String http = null; // the recent data as ready formatted http message 

        // start the server and wait for clients but async (automatically in own thread):
        protected async void startServerAsync(int port)
        {
            if (server != null)
            {
                print(TAG + " Close old Server " + port);
                server.Stop();
                server = null;
            }
            print(TAG + " Start new Server " + port);
            server = TcpListener.Create(port);
            server.Start();
            while (true)
            {
                print(TAG + " [Server] waiting for new client...");
                using (var tcpClient = await server.AcceptTcpClientAsync())
                {
                    try
                    {
                        print(TAG + " [Server] Client has connected");
                        using (var networkStream = tcpClient.GetStream())
                        using (var reader = new StreamReader(networkStream))
                        using (var writer = new StreamWriter(networkStream) { AutoFlush = true })
                        {
                            // read request first:
                            bool emptyLine = false;
                            bool firstLine = true;
                            while (!emptyLine)
                            {
                                var request = await reader.ReadLineAsync();
                                var line = request.ToString();
                                if (firstLine)
                                {
                                    print(TAG + " [Server] Request from client " + request);
                                    firstLine = false;
                                }
                                emptyLine = line.Length == 0;
                            }
                            // answer:
                            await writer.WriteLineAsync(http);
                            // close the connection:
                            tcpClient.Close();
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine(TAG + " [Server] client connection lost");
                    }
                }
            }
        }

        // get all new data and store it locally for faster access:
        public void updateData()
        {
            // read all data, but handle errors:
            double time = System.Math.Round(Planetarium.GetUniversalTime());

            double altitude = 0;
            try { altitude = vessel.altitude; }
            catch (Exception) { altitude = 0; }

            double srfSpeed = 0;
            try { srfSpeed = vessel.srfSpeed; }
            catch (Exception) { srfSpeed = 0; }

            var json =
                "{"
                + "\"time\":" + time + ", "
                + "\"altitude\":" + altitude + ", "
                + "\"srfSpeed\":" + srfSpeed
                + "}"
            ;


            http =
            "HTTP / 1.1 200 OK\r\n"
            + "Content-Type: application/json\r\n"
                + "Access-Control-Allow-Origin: *\r\n"
                + "Access-Control-Allow-Methods: POST, GET, OPTIONS\r\n"
                + "Access-Control-Allow-Headers: X-PINGOTHER, Content-Type\r\n"
                + "\r\n"
                + json
                + "\r\n"
                + "\r\n";
        }

        public void FixedUpdate()
        {
            updateData();
            // print(http);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            print(TAG + " OnStart " + state.ToString() + " state=" + ((int)state));

            if ((state & StartState.PreLaunch) != 0)
            {
                startServerAsync(2025);
            }
        }
    }
}