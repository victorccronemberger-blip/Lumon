using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace Lumon.HubWorld
{
    // A LAN room owns its six slots. Clients submit direction and readiness only.
    // Version-one messages are 80 bytes, with six fixed x/z/yaw records in a roster.
    public sealed class HubLanSession : MonoBehaviour
    {
        public const int Capacity = 6;
        private const int Port = 42636, FrameSize = 80;
        private const byte Join = 1, InputFrame = 2, Roster = 3, Full = 4;
        private const float Interval = 0.0667f;
        public static readonly string[] Callsigns = { "Aurora", "Órbita", "Vega", "Íris", "Lira", "Ceres" };
        private static readonly Vector4[] UniformDyes = {
            Vector4.zero, new(0.67f,1.08f,1.34f,1), new(1.71f,0.79f,0.65f,1),
            new(1.31f,0.79f,1.47f,1), new(0.81f,1.16f,0.62f,1), new(1.48f,0.99f,0.51f,1)
        };
        private static readonly Vector2[] Berths = {
            new(-1.61f,0.32f), new(-2.75f,1.03f), new(-0.47f,-0.40f),
            new(-1.77f,2.62f), new(-0.62f,1.90f), new(0.52f,1.19f)
        };

        private sealed class Peer : IDisposable
        {
            internal readonly TcpClient Client;
            internal readonly byte[] Receive = new byte[FrameSize];
            internal int Used, Slot = -1;
            internal float LastSeen = Time.realtimeSinceStartup;
            private Task write;

            internal Peer(TcpClient client) { Client = client; Client.NoDelay = true; }
            internal void Send(byte[] frame)
            {
                if (write != null && !write.IsCompleted) return;
                write?.GetAwaiter().GetResult();
                write = Client.GetStream().WriteAsync(frame, 0, FrameSize);
            }
            internal void Read(Action<byte[]> consume)
            {
                Socket socket = Client.Client;
                for (int frame = 0; frame < 5; frame++)
                {
                    if (!socket.Poll(0, SelectMode.SelectRead)) break;
                    int received = socket.Receive(Receive, Used, FrameSize - Used, SocketFlags.None);
                    if (received == 0) throw new EndOfStreamException();
                    Used += received;
                    if (Used != FrameSize) continue;
                    Used = 0;
                    if (Receive[0] != 'L' || Receive[1] != 'M' || Receive[2] != 1)
                        throw new InvalidDataException();
                    LastSeen = Time.realtimeSinceStartup;
                    consume(Receive);
                    if (Slot == -2) break;
                }
                if (Time.realtimeSinceStartup - LastSeen > (Slot < 0 ? 12f : 25f)) throw new IOException();
            }
            public void Dispose() => Client.Close();
        }

        private readonly List<Peer> guests = new();
        private readonly Vector3[] positions = new Vector3[Capacity], directions = new Vector3[Capacity];
        private readonly Vector3[] velocities = new Vector3[Capacity];
        private readonly float[] headings = new float[Capacity];
        private readonly SplatGuardian[] replicas = new SplatGuardian[Capacity];
        private HubSplatWorld world;
        private TcpListener listener;
        private Peer server;
        private TcpClient dialing;
        private Task connect;
        private float started, nextTick;
        private int mask, readyMask;
        private bool requestedReady;
        private int roomCharts, pendingChart, soloCharts;
        public int Charts => LocalSlot >= 0 ? roomCharts : soloCharts;
        internal int RosterState => mask | (readyMask << Capacity) | (Charts << (2 * Capacity));
        public void Observe(int chart)
        {
            if (chart != 1 && chart != 2) return;
            if (!WithinReach(world.GuardianPosition, chart)) return;
            if (LocalSlot < 0) soloCharts |= chart;
            else if (Hosting) roomCharts |= chart;
            else pendingChart = chart;
        }
        private bool WithinReach(Vector3 pos, int chart)
        {
            var spot = world.Spot(chart == 1 ? LumonStationId.WestScope : LumonStationId.EastScope);
            return spot != null && (pos - spot.approach).sqrMagnitude <= spot.radius * spot.radius;
        }

        public int LocalSlot { get; private set; } = -1;
        public bool Hosting => listener != null;
        public bool Active => Hosting || server != null || dialing != null;
        public string Notice { get; private set; } = "";
        public string Address { get; private set; } = "";
        public int Count { get { int count = 0; for (int i = 0; i < Capacity; i++) if (Occupied(i)) count++; return count; } }
        public bool Occupied(int i) => (mask & (1 << i)) != 0;
        public bool Ready(int i) => (readyMask & (1 << i)) != 0;
        internal Vector3 Position(int i) => i == LocalSlot ? world.GuardianPosition : replicas[i] != null ? replicas[i].transform.position : positions[i];
        private void Awake() => world = GetComponent<HubSplatWorld>();
        private void OnDisable() => Leave();

        public void Host()
        {
            if (Active) return;
            try
            {
                listener = new TcpListener(IPAddress.Any, Port);
                listener.Start(8);
                LocalSlot = 0;
                Allocate(0);
                PlaceLocal();
                var addresses = Array.FindAll(Dns.GetHostAddresses(Dns.GetHostName()),
                    ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip));
                Address = addresses.Length == 0 ? "127.0.0.1" : string.Join("  /  ", Array.ConvertAll(addresses, ip => ip.ToString()));
                Notice = "Sala aberta";
            }
            catch (SocketException) { Leave("Não foi possível abrir a sala nesta porta."); }
        }
        public void Enter(string address)
        {
            if (Active) return;
            if (!IPAddress.TryParse(address.Trim(), out IPAddress ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            { Notice = "Digite o IPv4 do anfitrião."; return; }
            try
            {
                dialing = new TcpClient(AddressFamily.InterNetwork);
                connect = dialing.ConnectAsync(ip, Port);
                started = Time.realtimeSinceStartup;
                Notice = "Conectando…";
            }
            catch (SocketException) { Leave("Não foi possível conectar."); }
        }
        public void ToggleReady()
        {
            if (LocalSlot < 0) return;
            requestedReady = !requestedReady;
            if (Hosting) SetReady(LocalSlot, requestedReady);
        }
        private void SetReady(int slot, bool value)
        { if (value) readyMask |= 1 << slot; else readyMask &= ~(1 << slot); }
        private void Allocate(int slot)
        {
            mask |= 1 << slot;
            directions[slot] = Vector3.zero;
            velocities[slot] = Vector3.zero;
            Vector2 p = Berths[slot];
            positions[slot] = new Vector3(p.x, world.GuardianPosition.y, p.y);
            headings[slot] = 195f;
            SetReady(slot, false);
        }
        private void PlaceLocal()
        {
            var actor = world.LocalGuardian;
            actor.ResetTravel();
            actor.NetworkDriven = !Hosting;
            actor.NetworkTarget = positions[LocalSlot];
            actor.NetworkYaw = headings[LocalSlot];
            actor.transform.SetPositionAndRotation(positions[LocalSlot], Quaternion.Euler(0, headings[LocalSlot], 0));
            actor.ClothDye = UniformDyes[LocalSlot];
        }
        public void Leave(string message = "")
        {
            listener?.Stop(); listener = null;
            foreach (Peer guest in guests) guest.Dispose();
            guests.Clear();
            server?.Dispose(); server = null;
            dialing?.Close(); dialing = null; connect = null;
            for (int i = 0; i < Capacity; i++) RemoveReplica(i);
            if (world != null && world.LocalGuardian != null)
            {
                world.LocalGuardian.NetworkDriven = false;
                world.LocalGuardian.ResetTravel();
                world.LocalGuardian.ClothDye = Vector4.zero;
            }
            LocalSlot = -1; roomCharts = pendingChart = readyMask = mask = 0; requestedReady = false;
            Address = ""; Notice = message;
        }
        private void RemoveReplica(int slot)
        {
            if (replicas[slot] != null) Destroy(replicas[slot].gameObject);
            replicas[slot] = null;
        }
        private static byte[] Message(byte type)
        {
            var frame = new byte[FrameSize];
            frame[0] = (byte)'L'; frame[1] = (byte)'M'; frame[2] = 1; frame[3] = type;
            return frame;
        }
        private static void Put(byte[] b, int offset, float v) => BitConverter.TryWriteBytes(new Span<byte>(b,offset,4),v);
        private static float Get(byte[] b, int offset)
        {
            float v = BitConverter.ToSingle(b, offset);
            if (float.IsNaN(v) || float.IsInfinity(v) || Mathf.Abs(v) > 1000f) throw new InvalidDataException();
            return v;
        }
        private void Update()
        {
            if (!Active) return;
            float now = Time.realtimeSinceStartup;
            bool tick = now >= nextTick;
            if (tick) nextTick = now + Interval;
            try
            {
                if (Hosting) RunHost(tick);
                else RunGuest(tick, now);
                SyncActors();
            }
            catch (Exception ex) when (ex is IOException || ex is SocketException || ex is ObjectDisposedException || ex is InvalidDataException)
            { Leave("Conexão encerrada. Você voltou ao modo solo."); }
        }
        private void RunHost(bool tick)
        {
            for (int n = 0; n < 4 && listener.Pending(); n++)
            {
                var peer = new Peer(listener.AcceptTcpClient());
                if (guests.Count >= 12) peer.Dispose(); else guests.Add(peer);
            }
            for (int j = guests.Count - 1; j >= 0; j--)
            {
                Peer p = guests[j];
                try
                {
                    p.Read(frame => ReadGuest(p,frame));
                    if (p.Slot == -2) { p.Dispose(); guests.RemoveAt(j); }
                }
                catch (Exception ex) when (ex is IOException || ex is SocketException || ex is InvalidDataException)
                {
                    if (p.Slot >= 0) { mask &= ~(1 << p.Slot); SetReady(p.Slot,false); }
                    p.Dispose(); guests.RemoveAt(j);
                }
            }
            positions[0] = world.GuardianPosition;
            headings[0] = world.LocalGuardian.transform.eulerAngles.y;
            for (int i = 1; i < Capacity; i++)
            {
                if (!Occupied(i)) continue;
                Vector3 delta = SplatGuardian.WalkDelta(ref velocities[i], directions[i], Time.deltaTime);
                if (delta.sqrMagnitude == 0f) continue;
                Vector3 next = world.Constrain(positions[i], positions[i] + delta);
                Vector3 travel = next - positions[i];
                if (travel.sqrMagnitude > 0f)
                    headings[i] = Mathf.MoveTowardsAngle(headings[i], Quaternion.LookRotation(travel).eulerAngles.y, 720f * Time.deltaTime);
                positions[i] = next;
            }
            if (!tick) return;
            for (int j = guests.Count - 1; j >= 0; j--)
            {
                Peer p = guests[j];
                if (p.Slot < 0) continue;
                var frame = Message(Roster);
                frame[4] = (byte)p.Slot; frame[5] = (byte)mask; frame[6] = (byte)readyMask; frame[7] = (byte)roomCharts;
                for (int i = 0; i < Capacity; i++)
                {
                    int offset = 8 + 12*i;
                    Put(frame,offset,positions[i].x); Put(frame,offset+4,positions[i].z); Put(frame,offset+8,headings[i]);
                }
                try { p.Send(frame); }
                catch (Exception ex) when (ex is IOException || ex is SocketException) { mask &= ~(1 << p.Slot); SetReady(p.Slot,false); p.Dispose(); guests.RemoveAt(j); }
            }
        }
        private void ReadGuest(Peer peer, byte[] frame)
        {
            if (peer.Slot == -1)
            {
                if (frame[3] != Join) throw new InvalidDataException();
                for (int i = 1; i < Capacity; i++)
                    if (!Occupied(i)) { peer.Slot = i; Allocate(i); return; }
                peer.Send(Message(Full)); peer.Slot = -2; return;
            }
            if (peer.Slot < 0 || frame[3] != InputFrame) throw new InvalidDataException();
            directions[peer.Slot] = Vector3.ClampMagnitude(new Vector3(Get(frame,8),0,Get(frame,12)),1f);
            SetReady(peer.Slot,frame[6] == 1);
            int chart = frame[7];
            if ((chart == 1 || chart == 2) && WithinReach(positions[peer.Slot],chart)) roomCharts |= chart;
        }
        private void RunGuest(bool tick, float now)
        {
            if (dialing != null)
            {
                if (now - started > 9f) { Leave("O anfitrião não respondeu."); return; }
                if (!connect.IsCompleted) return;
                connect.GetAwaiter().GetResult();
                server = new Peer(dialing); dialing = null; connect = null;
                server.Send(Message(Join));
            }
            server.Read(frame => {
                if (frame[3] == Full) { server.Slot = -2; Notice = "Sala cheia. As seis vagas estão ocupadas."; return; }
                if (frame[3] != Roster || frame[4] < 1 || frame[4] >= Capacity ||
                    (frame[5] & (1 << frame[4])) == 0) throw new InvalidDataException();
                bool entering = LocalSlot < 0;
                LocalSlot = server.Slot = frame[4]; mask = frame[5]; readyMask = frame[6]; roomCharts = frame[7] & 3;
                if ((roomCharts & pendingChart) != 0) pendingChart = 0;
                for (int i = 0; i < Capacity; i++)
                {
                    int offset = 8+12*i;
                    positions[i] = new Vector3(Get(frame,offset),world.GuardianPosition.y,Get(frame,offset+4));
                    headings[i] = Get(frame,offset+8);
                }
                if (entering) { PlaceLocal(); Notice = "Conectado à sala"; }
            });
            if (Notice.StartsWith("Sala cheia")) { Leave(Notice); return; }
            if (tick && LocalSlot >= 0)
            {
                Vector3 direction = world.LocalGuardian.ReadDirection();
                var frame = Message(InputFrame);
                frame[6] = (byte)(requestedReady ? 1 : 0); frame[7] = (byte)pendingChart;
                Put(frame,8,direction.x); Put(frame,12,direction.z); server.Send(frame);
            }
        }
        private void SyncActors()
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (!Occupied(i)) { RemoveReplica(i); continue; }
                if (i == LocalSlot && Hosting) continue;
                var actor = world.LocalGuardian;
                if (i != LocalSlot)
                {
                    if (replicas[i] == null)
                    {
                        replicas[i] = Instantiate(actor,positions[i],Quaternion.Euler(0,headings[i],0));
                        replicas[i].name = Callsigns[i];
                        replicas[i].NetworkDriven = true;
                        replicas[i].ClothDye = UniformDyes[i];
                    }
                    actor = replicas[i];
                }
                actor.NetworkTarget = positions[i]; actor.NetworkYaw = headings[i];
            }
        }
    }
}
