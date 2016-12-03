using UnityEngine;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

public class NetworkLayer : MonoBehaviour
{
    int myPort;
    TcpListener listener;
    TcpClient tcpclient;
    Thread serveTcpThread, epochThread, clientThread, netmanThread;
    public bool inGame, isServer;
    MultiplayerMenu menu;
    Process netmanProcess;
    public GameController controller;
    public int myid;
    class Client
    {
        public TcpClient conn;
        public DateTime lastmsg;
        public Thread thread;
        public string addr;
        public StreamReader sr;
        public StreamWriter sw;
    }
    Dictionary<string, Client> clients = new Dictionary<string, Client>();
    void Start()
    {
        controller = UnityEngine.Object.FindObjectOfType<GameController>();
        myPort = UnityEngine.Random.Range(10000, 60000);
    }
    public void Host()
    {
        var myip = Dns.GetHostAddresses(Dns.GetHostName()).Select(ip => ip.ToString()).Where(ip => !ip.Contains(":")).First();
        clients.Add(myip + ":" + myPort, new Client() { addr = myip });
        isServer = true;
        listener = new TcpListener(IPAddress.Any, myPort);
        listener.Start();
        serveTcpThread = new Thread(new ThreadStart(ListenTCP));
        serveTcpThread.Start();
        epochThread = new Thread(new ThreadStart(Epoch));
        epochThread.Start();
    }
    public Exception Connect(string hostport, MultiplayerMenu menu)
    {
        this.menu = menu;
        if (!hostport.Contains(":")) hostport = "127.0.0.1:" + hostport;
        try
        {
            tcpclient = new TcpClient();
            tcpclient.Connect(hostport.Split(':')[0], int.Parse(hostport.Split(':')[1]));
        }
        catch (Exception ex) { UnityEngine.Debug.Log(ex); return ex; }
        clientThread = new Thread(new ThreadStart(HeartBeat));
        clientThread.Start();
        return null;
    }
    public void StartGame()
    {
        try
        {
            epochThread.Abort();
        }
        catch (Exception ex) { UnityEngine.Debug.Log(ex); }
        var hostport = clients.Keys.Select(hp => ExtractHost(hp) + ":" + UnityEngine.Random.Range(10000, 60000)).ToArray();
        lock (this)
        {
            controller.InitTransform(clients.Count);
            controller.initPlayersInt = true;
            controller.initInt = clients.Count;
            var posstr = string.Join(" ", controller.position.Values.Select(p => p.Serialize()).ToArray());
            var cmd = "Start " + string.Join(" ", hostport);
            var id = 0;
            foreach (var client in clients.Values)
            {
                if (client.conn != null)
                    try
                    {
                        client.thread.Abort();
                        client.sw.WriteLine(cmd + " " + id);
                        client.sw.WriteLine(posstr);
                        client.sw.Flush();
                    }
                    catch (Exception ex) { UnityEngine.Debug.Log(ex); }
                id++;
            }
        }
        SetupNetman(hostport, 0);
        foreach (var client in clients.Values)
        {
            try
            {
                if (client.conn != null) client.conn.Close();
            }
            catch (Exception) { }
        }
        inGame = true;
    }
    void SetupNetman(string[] hostport, int id)
    {
        var start = new ProcessStartInfo();
        start.FileName = IsLinux() ? "netman" : "Z:\\netman.exe";
        start.UseShellExecute = false;
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        start.Arguments = string.Format("-N={0} -id={1} -port={2} -hostports={3} -retries=100", hostport.Length, id, ExtractPort(hostport[id]), string.Join(",", hostport));
        netmanProcess = Process.Start(start);
        netmanThread = new Thread(new ThreadStart(ReadNetman));
        netmanThread.Start();
        lock (netmanThread) Monitor.Wait(netmanThread);
    }
    void ReadNetman()
    {
        var sr = netmanProcess.StandardOutput;
        var sw = netmanProcess.StandardInput;
        while (true)
        {
            var line = sr.ReadLine();
            UnityEngine.Debug.Log(line);
            var tokens = line.Split(' ');
            try
            {
                if (tokens[0] == "Ready") lock (netmanThread) Monitor.Pulse(netmanThread);
                else if (tokens[0] == "Msg")
                {
                    if (tokens[1] == "Position")
                    {
                        var id = int.Parse(tokens[2]);
                        if (id == myid) continue;
                        var pos = tokens[3].DeserializeVector3();
                        pos.y -= 1.0f;
                        var rot = tokens[4].DeserializeVector3();
                        lock (controller.position)
                        {
                            controller.position[id] = pos;
                            controller.rotation[id] = rot;
                        }
                    }
                }
            }
            catch (Exception ex) { UnityEngine.Debug.Log(ex); }
        }
    }
    void ListenTCP()
    {
        while (true)
        {
            try
            {
                var client = listener.AcceptTcpClient();
                lock (this)
                {
                    var myip = client.Client.RemoteEndPoint.ToString();
                    if (myip.StartsWith("127.0.0.1"))
                        myip = myip.Replace("127.0.0.1", Dns.GetHostAddresses(Dns.GetHostName()).Select(ip => ip.ToString()).Where(ip => !ip.Contains(":")).First());
                    var entry = new Client() { conn = client, lastmsg = DateTime.Now, addr = myip };
                    entry.thread = new Thread(new ParameterizedThreadStart(ServeTCP));
                    entry.thread.Start(entry);
                    clients.Add(myip, entry);
                }
            }
            catch (Exception ex) { UnityEngine.Debug.Log(ex); return; }
        }
    }
    void ServeTCP(object obj)
    {
        var client = obj as Client;
        var ns = client.conn.GetStream();
        client.sr = new StreamReader(ns);
        client.sw = new StreamWriter(ns);
        while (true)
        {
            try
            {
                var line = client.sr.ReadLine();
                if (line.StartsWith("Ping")) client.lastmsg = DateTime.Now;
                lock (this)
                    client.sw.WriteLine("List " + string.Join(" ", clients.Keys.ToArray()));
                client.sw.Flush();
            }
            catch (Exception ex) { UnityEngine.Debug.Log(ex); return; }
        }
    }
    void Epoch()
    {
        while (true)
        {
            lock (this)
            {
                var remove = clients.Where(c => c.Value.conn != null && DateTime.Now - c.Value.lastmsg >= TimeSpan.FromSeconds(3)).Select(c => c.Key).ToList();
                remove.ForEach(ck =>
                {
                    try
                    {
                        clients[ck].thread.Abort();
                        clients[ck].conn.Close();
                        clients.Remove(ck);
                    }
                    catch (Exception) { }
                });
            }
            Thread.Sleep(1000);
        }
    }
    void HeartBeat()
    {
        var ns = tcpclient.GetStream();
        var sr = new StreamReader(ns);
        var sw = new StreamWriter(ns);
        while (true)
        {
            try
            {
                sw.WriteLine("Ping");
                sw.Flush();
                var line = sr.ReadLine().Split(' ');
                if (line[0] == "List" || line[0] == "Start")
                {
                    clients.Clear();
                    lock (this)
                        for (int i = 1; i < line.Length; i++)
                            if (line[i].Contains(":"))
                                clients.Add(line[i], null);
                }
                if (line[0] == "Start")
                {
                    myid = int.Parse(line[line.Length - 1]);
                    var posstr = sr.ReadLine();
                    UnityEngine.Debug.Log(posstr);
                    controller.initInt = myid;
                    controller.initString = posstr;
                    controller.initPlayersString = true;
                    SetupNetman(clients.Keys.ToArray(), myid);
                    inGame = true;
                    menu.triggerStart = true; ;
                    return;
                }
                Thread.Sleep(500);
            }
            catch (Exception ex) { menu.fail(ex); UnityEngine.Debug.Log(ex); return; }
        }
    }
    bool IsLinux()
    {
        int p = (int)Environment.OSVersion.Platform;
        return (p == 4) || (p == 6) || (p == 128);
    }
    string ExtractPort(string hostport)
    {
        var tokens = hostport.Split(':');
        if (tokens.Length < 2) return "";
        return tokens[1];
    }
    string ExtractHost(string hostport)
    {
        var tokens = hostport.Split(':');
        if (tokens.Length < 2) return "";
        return tokens[0];
    }
    public string GetIPString()
    {
        return string.Join(",", Dns.GetHostAddresses(Dns.GetHostName()).Select(ip => ip.ToString()).Where(ip => !ip.Contains(":")).Select(ip => ip + ":" + myPort).ToArray());
    }
    public string GetClientList()
    {
        lock (this)
            return string.Join("\r\n", clients.Keys.ToArray());
    }
    public void Broadcast(string msg)
    {
        if (netmanProcess != null)
            lock (netmanProcess.StandardInput)
            {
                netmanProcess.StandardInput.WriteLine("Broadcast");
                netmanProcess.StandardInput.WriteLine();
                netmanProcess.StandardInput.WriteLine(msg);
                netmanProcess.StandardInput.Flush();
            }
    }
    public void Stop()
    {
        try { netmanThread.Abort(); } catch (Exception) { }
        try { netmanProcess.Kill(); } catch (Exception) { }
    }
}
