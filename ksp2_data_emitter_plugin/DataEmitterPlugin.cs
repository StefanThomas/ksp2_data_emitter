using System;
using System.IO;
using System.Net.Sockets;

namespace ksp2_data_emitter_plugin
{
    public class TestPlugin : PartModule
    {
        const String TAG = "---TESTPLUGIN---";

        static TcpListener server = null; // the server, use static to avoid multiple servers
        String message = null; // recent message
        String http = null;

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
            while(true)
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
            String time = Math.Round(Planetarium.GetUniversalTime()).ToString();

            String altitude = "-";
            try { altitude = Math.Round(vessel.altitude).ToString(); }
            catch (Exception) { altitude = "error"; }

            String srfSpeed = "-";
            try { srfSpeed = Math.Round(vessel.srfSpeed).ToString(); }
            catch (Exception) { srfSpeed = "error"; }

            String verticalSpeed = "-";
            try { verticalSpeed = Math.Round(vessel.verticalSpeed).ToString(); }
            catch (Exception) { verticalSpeed = "error"; }

            String horizontalSrfSpeed = "-";
            try { horizontalSrfSpeed = Math.Round(vessel.horizontalSrfSpeed).ToString(); }
            catch (Exception) { horizontalSrfSpeed = "error"; }

            const String delimiter = ";";
            message =
                "time:" + time + delimiter
                + "alt:" + altitude + delimiter
                + "srfspeed:" + srfSpeed + delimiter
                + "verticalSpeed:" + verticalSpeed + delimiter
                + "horizontalSrfSpeed:" + horizontalSrfSpeed + delimiter
            ;


            http =
                "HTTP / 1.1 200 OK\r\n" +
                "Content - Type: text / html\r\n" +
                "Connection: close\r\n" +
                "\r\n" +
                "<!DOCTYPE html>" +
                "<html>" +
                "<head><meta http-equiv=\"refresh\" content=\"1\"></head>" +
                "<body>" + message + "</body>" +
                "</html>\r\n" +
                "\r\n";
        }

        public void FixedUpdate()
        {
            updateData();
            // print(message);
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