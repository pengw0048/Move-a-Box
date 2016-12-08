// Network Layer
// This script does two things:
// 1. Use TCP server and clients to initially set up connections and propagate client list &
//    init params to everyone.
// 2. Communicate with netman, decoding messages and dispatch them to different components in
//    the game. It also provides other scripts with interfaces to broadcast and propose values.

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

public class NetworkLayer : MonoBehaviour
{
    int myPort;  // port for temp tcp that I will listen on
    TcpListener listener;  // listen for tcp connection as a temp "server"
    TcpClient tcpclient;  // temp tcp "client"
    Thread serveTcpThread, epochThread, clientThread, netmanThread, syncWorldThread;
    public bool inGame, isServer;
    public MultiplayerMenu menu;
    Process netmanProcess;
    GameController controller;
    public int myid;  // my nodeid
    string myIp;
    PopupMessageManager popman;
    public Toggle consoleOnlyToggle;
    class Client  // represents a node in the connection establishment stage
    {
        public TcpClient conn;
        public DateTime lastmsg;  // to check if alive
        public Thread thread;
        public string addr;
        public StreamReader sr;
        public StreamWriter sw;
    }
    Dictionary<string, Client> clients = new Dictionary<string, Client>();  // maps hostport to client
    void Start()
    {
		// there's no easy way in *nux to get an "external" ip, so let's just assume ...
		if (IsLinux ()) myIp = GetIP();
        else myIp = Dns.GetHostAddresses(Dns.GetHostName()).Select(ip => ip.ToString()).Where(ip => !ip.Contains(":")).First();  // for windows
        controller = UnityEngine.Object.FindObjectOfType<GameController>();
        popman = FindObjectOfType<PopupMessageManager>();
        myPort = UnityEngine.Random.Range(10000, 60000);
        StartCoroutine(SyncWorld());
    }
    // act as a temp "server" in tcp for the initial multiplayer mode setup
    public void Host()
    {
        controller.isMultiplayer = true;
        clients.Add(myIp + ":" + myPort, new Client() { addr = myIp });
        isServer = true;
        // listen on myport, waiting for incoming connections, and monitor if connected nodes are still alive
        listener = new TcpListener(IPAddress.Any, myPort);
        listener.Start();
        serveTcpThread = new Thread(new ThreadStart(ListenTCP));
        serveTcpThread.Start();
        epochThread = new Thread(new ThreadStart(Epoch));
        epochThread.Start();
    }
    // act as a temp "client" in initial setup, returns any error or null if succeed
    public Exception Connect(string hostport)
    {
        controller.isMultiplayer = true;
        if (!hostport.Contains(":")) hostport = "127.0.0.1:" + hostport; // for test simplicity
        try
        {
            tcpclient = new TcpClient();
            tcpclient.Connect(hostport.Split(':')[0], int.Parse(hostport.Split(':')[1]));
        }
        catch (Exception ex) { UnityEngine.Debug.Log(ex); return ex; }
        // ready to go, start heartbeat thread
        clientThread = new Thread(new ThreadStart(HeartBeat));
        clientThread.Start();
        return null;
    }
    // as a server: tell everyone player list and init positions etc and let them start
    public void StartGame()
    {
        try
        {
            epochThread.Abort();
        }
        catch (Exception ex) { UnityEngine.Debug.Log(ex); }
        // pick host:{random port} for all nodes, which will be fed into netman
        var hostport = clients.Keys.Select(hp => ExtractHost(hp) + ":" + UnityEngine.Random.Range(10000, 60000)).ToArray();
        lock (this)
        {
            // initialize starting positions of players
            controller.InitTransform(clients.Count);
            controller.initPlayersInt = true;
            controller.initInt = clients.Count;
            var posstr = string.Join(" ", controller.position.Values.Select(p => p.Serialize()).ToArray());  // player start positions
            var cmd = "Start " + string.Join(" ", hostport);  // through temp tcp tell others to start and connect to all players
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
        SetupNetman(hostport, 0);  // start netman
        // clean up
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
    // run netman with given params
    void SetupNetman(string[] hostport, int id)
    {
        var start = new ProcessStartInfo();
        start.FileName = IsLinux() ? "netman" : "Z:\\netman.exe";
        start.Arguments = string.Format("-N={0} -id={1} -port={2} -hostports={3} -retries=100", hostport.Length, id, ExtractPort(hostport[id]), string.Join(",", hostport));
        if (!consoleOnlyToggle.isOn)
        {
            start.UseShellExecute = false;
            start.RedirectStandardInput = true;
            start.RedirectStandardOutput = true;
            start.CreateNoWindow = true;
        }
        netmanProcess = Process.Start(start);
        if (!consoleOnlyToggle.isOn)
        {
            netmanThread = new Thread(new ThreadStart(ReadNetman));
            netmanThread.Start();
            lock (netmanThread) Monitor.Wait(netmanThread);  // wait Ready message from netman
        }
        else Process.GetCurrentProcess().Kill();
    }
    // a thread function that reads lines from netman and acts accordingly
    void ReadNetman()
    {
        var sr = netmanProcess.StandardOutput;
        var sw = netmanProcess.StandardInput;
        while (true)
        {
            var line = sr.ReadLine();
            var tokens = line.Split(' ');  // the first string specifies message type
            try
            {
                if (tokens[0] == "Ready")
                {
                    lock (netmanThread) Monitor.Pulse(netmanThread);  // tell SetupNetman all nodes are connected
                    lock (popman.msgreq) popman.msgreq.Add("Game starts");
                }
                else if (tokens[0] == "Msg")  // broadcast message, the second token is the real message type
                {
                    if (tokens[1] == "Position")  // set the position & rotation of a remote player
                    {
                        var id = int.Parse(tokens[2]);  // player id (=connid)
                        if (id == myid) continue;
                        var pos = tokens[3].DeserializeVector3();
                        pos.y -= 1.0f;
                        var rot = tokens[4].DeserializeVector3();
                        lock (controller.position)  // add it to the request list of game controller
                        {
                            controller.position[id] = pos;
                            controller.rotation[id] = rot;
                        }
                    }
                    else if (tokens[1] == "Object")  // set the transform of a (holding) object
                    {
                        var pid = int.Parse(tokens[2]);
                        var oid = int.Parse(tokens[3]);
                        if (pid == myid) continue;
                        lock (controller.movereq)  // add the request to the request list of game controller
                            controller.movereq.Add(new GameController.MoveObjectRequest() { oid = oid, position = tokens[4].DeserializeVector3(), rotation = tokens[5].DeserializeVector3(), scale = tokens[6].DeserializeVector3() });
                    }
                    else if (tokens[1] == "PutDown")  // put down a holding object
                    {
                        var pid = int.Parse(tokens[2]);
                        var oid = int.Parse(tokens[3]);
                        if (pid == myid) continue;
                        lock (controller.putreq)
                            controller.putreq.Add(oid);
                    }
                }
                else if (tokens[0] == "ProposeError")  // put an error message into the "channel" of Propose()
                {
                    proposeResponse = "error";
                    lock (proposeResponseMonitor) Monitor.Pulse(proposeResponseMonitor);
                }
                else if (tokens[0] == "ProposeOK")  // put the committed value into that channel
                {
                    proposeResponse = line.Substring(line.IndexOf(' ') + 1);
                    lock (proposeResponseMonitor) Monitor.Pulse(proposeResponseMonitor);
                }
                else if (tokens[0] == "Lost")  // netman detects connection lost
                {
                    lock (popman.msgreq) popman.msgreq.Add("Lost connection to " + tokens[1]);
                    lock (proposeResponseMonitor) Monitor.Pulse(proposeResponseMonitor);
                }
                else if (tokens[0] == "#")  // this can be ignored
                {
                    //lock (popman.msgreq) popman.msgreq.Add(line.Substring(2));
                }
                else if (tokens[0] == "Fatal")  // netman fails, tell popman (he will freeze the world)
                {
                    lock (popman.msgreq) popman.msgreq.Add(line);
                    sw.WriteLine("Bye");
                    sw.Flush();
                    lock (proposeResponseMonitor) Monitor.Pulse(proposeResponseMonitor);
                }
                else if (tokens[0] == "Paxos")  // a value has just been committed through Paxos
                {
                    if (tokens[1] == "sync")  // request to sync the game world, deserialize here to reduce pressure on game thread
                    {
                        var pid = int.Parse(tokens[2]);
                        if (pid == myid) continue;
                        var reqs = new List<GameController.SyncRequest>();
                        foreach (var sec in tokens[3].Split(';'))
                        {
                            if (sec == "") continue;
                            var comp = sec.Split('/');
                            var id = int.Parse(comp[0]);
                            var pos = comp[1].DeserializeVector3();
                            var rot = comp[2].DeserializeVector3();
                            var v = comp[3].DeserializeVector3();
                            var w = comp[4].DeserializeVector3();
                            reqs.Add(new GameController.SyncRequest() { angularVelocity = w, oid = id, position = pos, rotation = rot, velocity = v });
                        }
                        lock (controller.syncreq) controller.syncreq.AddRange(reqs);
                    }
                    else if (tokens[2] == "Pickup")  // global agreement to pickup (own, hold) an object
                    {
                        var pid = int.Parse(tokens[3]);
                        var oid = int.Parse(tokens[4]);
                        if (pid == myid) continue;
                        lock (controller.pickreq)
                            controller.pickreq.Add(oid);
                    }
                    else if (tokens[2] == "TakeOne")  // global agreement to take one object from a resource generator
                    {
                        var pid = int.Parse(tokens[3]);
                        var gid = int.Parse(tokens[4]);
                        var oid = int.Parse(tokens[5]);
                        if (pid == myid) continue;
                        lock (controller.takereq)
                            controller.takereq.Add(new GameController.TakeOneRequest() { gid = gid, oid = oid, pid = pid });
                    }
                }
            }
            catch (Exception ex) { UnityEngine.Debug.Log(ex); lock (proposeResponseMonitor) Monitor.Pulse(proposeResponseMonitor);}
        }
    }
    // a thread function used by the temp tcp server. it accepts connections and spawn threads to server clients
    void ListenTCP()
    {
        while (true)
        {
            try
            {
                var client = listener.AcceptTcpClient();
                lock (this)
                {
                    var ip = client.Client.RemoteEndPoint.ToString();
                    if (ip.StartsWith("127.0.0.1"))
                        ip = ip.Replace("127.0.0.1", myIp);
                    var entry = new Client() { conn = client, lastmsg = DateTime.Now, addr = ip };
                    entry.thread = new Thread(new ParameterizedThreadStart(ServeTCP));
                    entry.thread.Start(entry);
                    clients.Add(ip, entry);
                }
            }
            catch (Exception ex) { UnityEngine.Debug.Log(ex); return; }
        }
    }
    // a thread function for the temp server to exchange info with a client (obj = a TcpClient)
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
                if (line.StartsWith("Ping")) client.lastmsg = DateTime.Now;  // update timestamp of last message
                lock (this)
                    client.sw.WriteLine("List " + string.Join(" ", clients.Keys.ToArray()));  // send client list
                client.sw.Flush();
            }
            catch (Exception ex) { UnityEngine.Debug.Log(ex); return; }
        }
    }
    // a thread function for server to detect connection loss
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
    // a thread function for the client to send heartbeat to server and wait for client list & instruction to start game
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
                if (line[0] == "List" || line[0] == "Start")  // update client list
                {
                    clients.Clear();
                    lock (this)
                        for (int i = 1; i < line.Length; i++)
                            if (line[i].Contains(":"))
                                clients.Add(line[i], null);

                }
                if (line[0] == "Start")  // got instruction to start, set up netman
                {
                    myid = int.Parse(line[line.Length - 1]);
                    var posstr = sr.ReadLine();
                    controller.initInt = myid;
                    controller.initString = posstr;
                    controller.initPlayersString = true;
                    SetupNetman(clients.Keys.ToArray(), myid);
                    inGame = true;
                    menu.triggerStart = true;
                    return;
                }
                Thread.Sleep(500);
            }
            catch (Exception ex) { menu.fail(ex); UnityEngine.Debug.Log(ex); return; }
        }
    }
    // helper functions

    // this one from http://stackoverflow.com/questions/5116977/how-to-check-the-os-version-at-runtime-e-g-windows-or-linux-without-using-a-con
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
        return myIp + ":" + myPort;
    }

    // exporting functions
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
    // use a string variable and a monitor to simulate a go channel
    string proposeResponse;
    object proposeResponseMonitor = new object();
    public bool Propose(string key, string value)
    {
        if (netmanProcess != null)
            lock (netmanProcess.StandardInput)
            {
                netmanProcess.StandardInput.WriteLine("Propose");
                netmanProcess.StandardInput.WriteLine(key);
                netmanProcess.StandardInput.WriteLine(value);
                netmanProcess.StandardInput.Flush();
            }
        else return true;
        lock (proposeResponseMonitor) Monitor.Wait(proposeResponseMonitor);
        return value == proposeResponse;
    }
    public void Stop()
    {
        StopAllCoroutines();
        try { netmanThread.Abort(); } catch (Exception) { }
        try { syncWorldThread.Abort(); } catch (Exception) { }
        try { netmanProcess.Kill(); } catch (Exception) { }
    }
    void OnApplicationQuit()
    {
        Stop();
    }
    // at random intervals attempt to become a one-time leader and send the status of the world to everyone else
    IEnumerator SyncWorld()
    {
        var round = 0;
        while (true)
        {
            // wait for a random interval, considering the number of clients (so always ~0.3s per global sync)
            yield return new WaitForSecondsRealtime(clients.Count / 5.0f + UnityEngine.Random.Range(0f, 0.25f));
            if (inGame)
            {
                var sb = new StringBuilder();
                sb.Append(myid + " ");
                foreach (var item in FindObjectsOfType<Pickupable>())
                {
                    if (item.gameObject.transform.position.y < -9f) continue;  // ignore objects fallen to hell
                    if (round % 10 == 0 || item.gameObject.GetComponent<Rigidbody>().velocity.magnitude > 0.1f)  // only occasionally include static objects
                        sb.AppendFormat("{0}/{1}/{2}/{3}/{4};", item.id, item.gameObject.transform.position.Serialize(), item.gameObject.transform.rotation.eulerAngles.Serialize(), item.gameObject.GetComponent<Rigidbody>().velocity.Serialize(), item.gameObject.GetComponent<Rigidbody>().angularVelocity.Serialize());
                }
                Propose("sync", sb.ToString());
            }
            round = (round + 1) % 10;
        }
    }
	string GetIP()
	{
		using (var sock = new Socket (AddressFamily.InterNetwork, SocketType.Dgram, 0)) {
			sock.Connect ("10.0.2.4", 65530);
			var ep = sock.LocalEndPoint as IPEndPoint;
			return ep.Address.ToString ();
		}
	}
}
