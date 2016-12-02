using UnityEngine;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System;
using System.Linq;
using System.Collections.Generic;

public class NetworkLayer : MonoBehaviour {
    int myPort;
    TcpListener listener;
    TcpClient tcpclient;
    Thread serveTcpThread, epochThread, clientThread;
    bool inGame, isServer;
    class Client
    {
        public TcpClient conn;
        public DateTime lastmsg;
        public Thread thread;
        public string addr;
    }
    Dictionary<string, Client> clients = new Dictionary<string, Client>();
    void Start()
    {
        myPort = UnityEngine.Random.Range(10000, 65535);
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
    public Exception Connect(string hostport)
    {
        try
        {
            tcpclient = new TcpClient();
            tcpclient.Connect(hostport.Split(':')[0], int.Parse(hostport.Split(':')[1]));
        }
        catch (Exception ex) { Debug.Log(ex); return ex; }
        clientThread = new Thread(new ThreadStart(HeartBeat));
        clientThread.Start();
        return null;
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
                    if (myip == "127.0.0.1") myip = Dns.GetHostAddresses(Dns.GetHostName()).Select(ip => ip.ToString()).Where(ip => !ip.Contains(":")).First();
                    var entry = new Client() { conn = client, lastmsg = DateTime.Now, addr = myip };
                    entry.thread = new Thread(new ParameterizedThreadStart(ServeTCP));
                    entry.thread.Start(entry);
                    clients.Add(client.Client.RemoteEndPoint.ToString(), entry);
                }
            }
            catch (Exception ex) { Debug.Log(ex); return; }
        }
    }
    void ServeTCP(object obj)
    {
        var client = obj as Client;
        var ns = client.conn.GetStream();
        var sr = new StreamReader(ns);
        var sw = new StreamWriter(ns);
        while (true)
        {
            try
            {
                var line = sr.ReadLine();
                if (line.StartsWith("Ping")) client.lastmsg = DateTime.Now;
                lock (this)
                    sw.WriteLine("List " + string.Join(" ", clients.Keys.ToArray()));
                sw.Flush();
            }
            catch (Exception ex) { Debug.Log(ex); return; }
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
                if (line[0] == "List")
                {
                    clients.Clear();
                    lock (this)
                        for (int i = 1; i < line.Length; i++)
                            clients.Add(line[i], null);
                }
                Debug.Log(line);
                Thread.Sleep(500);
            }
            catch (Exception ex) { Debug.Log(ex); return; }
        }
    }
    public string GetIPString()
    {
        return string.Join(",", Dns.GetHostAddresses(Dns.GetHostName()).Select(ip => ip.ToString()).Where(ip => !ip.Contains(":")).Select(ip => ip + ":" + myPort).ToArray());
    }
    public string GetClientList()
    {
        Debug.Log(clients.Keys.ToArray());
        lock (this)
            return string.Join("\r\n", clients.Keys.ToArray());
    }
}
