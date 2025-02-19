using CommNet.Network;
using Smooth.Algebraics;
using System;
using System.IO;
using System.Net.Sockets;
using static KSP.UI.UITransitionBase;
using System.Runtime.Remoting.Messaging;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Linq;
using static TMPro.TMP_DefaultControls;

namespace ksp2_data_emitter_plugin
{
    public class TestPlugin : PartModule
    {
        const string TAG = "---TESTPLUGIN---";
        const int serverPort = 2025;

        static TcpListener server = null; // the server, use static to avoid multiple servers
        string http = null; // the recent data as ready formatted http message 

        //Editable Fields
        [KSPField(guiActive = false, isPersistant = false)]
        public string Propellants = "ElectricCharge, MonoPropellant, Liquid Fuel, Oxidizer";

        //Creating Lists
        public List<string> prop = new List<string>();

        public Dictionary<string, object> values = new Dictionary<string, object>();

        protected void stopServer()
        {
            if (server != null)
            {
                print(TAG + " Stop Server");
                try
                {
                    server.Stop();
                }
                catch (Exception) { }
                server = null;
            }
        }

        // start the server and wait for clients but async (automatically in own thread):
        protected async void startServerAsync()
        {
            stopServer();
            print(TAG + " Start new Server " + serverPort);
            server = TcpListener.Create(serverPort);
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

        public string jsonValue(string name, object value)
        {
            return "\"" + name + "\":" + (value.GetType() == typeof(string) ? ("\"" + value + "\"") : value.ToString());
        }

        // get all new data and store it locally for faster access:
        public void updateData()
        {
            string json = "{\n";
            bool jsonEmpty = true;

            // clear old values:
            values.Clear();

            // fill global data:
            values["time"] = System.Math.Round(Planetarium.GetUniversalTime());

            try { values["altitude"] = vessel.altitude; } catch (Exception) { }

            // fill the propellant info:
            char[] spearator = { ',', ';' };
            prop = Propellants.Split(spearator, 2).ToList();
            foreach (PartResource resource in part.Resources)
            {
                if (prop.Contains(resource.resourceName))
                {
                    try { values[resource.resourceName + "_amount"] = resource.amount; } catch (Exception) { }
                    try { values[resource.resourceName + "_max"] = resource.maxAmount; } catch (Exception) { }
                }
            }

            // fill all data into the json string:
            foreach (KeyValuePair<string, object> v in values)
            {
                if (jsonEmpty) jsonEmpty = false; else json += ",\n";
                json += " " + jsonValue(v.Key, v.Value);
            }
            json += "\n}";
            print(json);

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

            if ((state & StartState.Editor) != 0)
            {
                stopServer();
            }
            else
            if ((state & StartState.PreLaunch) != 0)
            {
                startServerAsync();
            }
        }

        public override void OnInactive()
        {
            base.OnInactive();
            print(TAG + " OnInactive ");
        }

        public void OnDestroy()
        {
            print(TAG + " OnDestroy ");
            stopServer();
        }
    }
}