using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine;
using Il2CppMonomiPark.SlimeRancher.Player.CharacterController;

[assembly: MelonInfo(typeof(SR2GyroAim.GyroMod), "SR2 Gyro Aim", "1.0.0", "FunkyByte1")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SR2GyroAim
{
    public class GyroMod : MelonMod
    {
        // Config
        private MelonPreferences_Category _config;
        private MelonPreferences_Entry<float> _sensitivityPan;
        private MelonPreferences_Entry<float> _sensitivityTilt;
        private MelonPreferences_Entry<float> _sensitivityTwist;
        private MelonPreferences_Entry<bool>  _invertPan;
        private MelonPreferences_Entry<bool>  _invertTilt;
        private MelonPreferences_Entry<bool>  _invertTwist;
        private MelonPreferences_Entry<float> _deadzone;

        // Cemuhook UDP client
        private UdpClient _udp;
        private IPEndPoint _serverEndPoint;
        private Thread _receiveThread;
        private bool _running = false;

        // Latest gyro values in deg/s (written by receive thread, read by main thread)
        private volatile float _gyroPitch = 0f;
        private volatile float _gyroYaw   = 0f;
        private volatile float _gyroRoll  = 0f;

        // Camera reference
        private SRCameraController _camera;

        public override void OnInitializeMelon()
        {
            // Set up config — Starlight picks this up automatically for its mod menu
            _config = MelonPreferences.CreateCategory("SR2GyroAim", "SR2 Gyro Aim");
            _sensitivityPan   = _config.CreateEntry("SensitivityPan",   1.5f,  "Pan Sensitivity",   "Left/right panning speed. Set to 0 to disable.");
            _sensitivityTilt  = _config.CreateEntry("SensitivityTilt",  1.5f,  "Tilt Sensitivity",  "Up/down tilt speed. Set to 0 to disable.");
            _sensitivityTwist = _config.CreateEntry("SensitivityTwist", 0f,    "Twist Sensitivity", "Left/right twist speed. Set to 0 to disable.");
            _invertPan        = _config.CreateEntry("InvertPan",        false, "Invert Pan",        "Invert left/right panning.");
            _invertTilt       = _config.CreateEntry("InvertTilt",       false, "Invert Tilt",       "Invert up/down tilt.");
            _invertTwist      = _config.CreateEntry("InvertTwist",      false, "Invert Twist",      "Invert left/right twist.");
            _deadzone         = _config.CreateEntry("Deadzone",         1.5f,  "Deadzone",          "Minimum gyro movement (deg/s) before input is registered. Increase if camera drifts when holding still.");

            try
            {
                _udp = new UdpClient();
                _serverEndPoint = new IPEndPoint(IPAddress.Loopback, 26760);
                _udp.Client.ReceiveTimeout = 1000;

                SendSubscribeRequest();

                _running = true;
                _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
                _receiveThread.Start();

                MelonLogger.Msg("SR2 Gyro Aim started. Cemuhook client listening on 127.0.0.1:26760");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Failed to start Cemuhook client: {e.Message}");
            }
        }

        private void SendSubscribeRequest()
        {
            byte[] packet = new byte[28];

            // Magic: "DSUC" (client)
            packet[0] = (byte)'D';
            packet[1] = (byte)'S';
            packet[2] = (byte)'U';
            packet[3] = (byte)'C';

            // Protocol version: 1001
            BitConverter.GetBytes((ushort)1001).CopyTo(packet, 4);

            // Length of packet without header (28 - 16 = 12)
            BitConverter.GetBytes((ushort)12).CopyTo(packet, 6);

            // CRC32 placeholder (filled below)
            BitConverter.GetBytes((uint)0).CopyTo(packet, 8);

            // Client ID
            BitConverter.GetBytes((uint)12345678).CopyTo(packet, 12);

            // Message type: 0x100002 = controller data request
            BitConverter.GetBytes((uint)0x100002).CopyTo(packet, 16);

            // Payload: flag=0 (subscribe to all), slot=0, MAC=000000000000
            packet[20] = 0;
            packet[21] = 0;

            uint crc = Crc32(packet);
            BitConverter.GetBytes(crc).CopyTo(packet, 8);

            _udp.Send(packet, packet.Length, _serverEndPoint);
        }

        private void ReceiveLoop()
        {
            DateTime lastSubscribe = DateTime.Now;

            while (_running)
            {
                try
                {
                    // Re-subscribe every 3 seconds as required by the protocol
                    if ((DateTime.Now - lastSubscribe).TotalSeconds > 3)
                    {
                        SendSubscribeRequest();
                        lastSubscribe = DateTime.Now;
                    }

                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udp.Receive(ref remote);

                    if (data.Length < 100) continue;

                    // Check magic "DSUS" (server message)
                    if (data[0] != 'D' || data[1] != 'S' || data[2] != 'U' || data[3] != 'S') continue;

                    // Message type at offset 16
                    uint msgType = BitConverter.ToUInt32(data, 16);
                    if (msgType != 0x100002) continue;

                    // Gyro offsets relative to payload start (byte 20):
                    // pitch=68, yaw=72, roll=76 — values in deg/s
                    _gyroPitch = BitConverter.ToSingle(data, 20 + 68);
                    _gyroYaw   = BitConverter.ToSingle(data, 20 + 72);
                    _gyroRoll  = BitConverter.ToSingle(data, 20 + 76);
                }
                catch (SocketException)
                {
                    // Timeout is normal — just loop and resubscribe
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"Receive error: {e.Message}");
                }
            }
        }

        private static float ApplyDeadzone(float value, float deadzone)
        {
            return Mathf.Abs(value) < deadzone ? 0f : value;
        }

        public override void OnUpdate()
        {
            if (_camera == null)
            {
                _camera = GameObject.FindObjectOfType<SRCameraController>();
                return;
            }

            float dt       = Time.deltaTime;
            float deadzone = _deadzone.Value;

            float pan   = ApplyDeadzone(_gyroRoll,  deadzone);
            float tilt  = ApplyDeadzone(_gyroPitch, deadzone);
            float twist = ApplyDeadzone(_gyroYaw,   deadzone);

            float invertPan   = _invertPan.Value   ? 1f : -1f;
            float invertTilt  = _invertTilt.Value  ? 1f : -1f;
            float invertTwist = _invertTwist.Value ? -1f : 1f;

            float yawDelta   = (pan * _sensitivityPan.Value * invertPan + twist * _sensitivityTwist.Value * invertTwist) * dt;
            float pitchDelta = tilt * _sensitivityTilt.Value * invertTilt * dt;

            if (Mathf.Abs(yawDelta) < 0.001f && Mathf.Abs(pitchDelta) < 0.001f) return;

            // Apply yaw by rotating the planar direction vector
            Vector3 dir = _camera._planarDirection;
            dir = Quaternion.AngleAxis(yawDelta, Vector3.up) * dir;
            _camera._planarDirection = dir;

            // Apply pitch
            _camera._targetVerticalAngle += pitchDelta;
        }

        public override void OnDeinitializeMelon()
        {
            _running = false;
            _udp?.Close();
            _receiveThread?.Join(500);
        }

        // Standard CRC32 implementation
        private static uint Crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
            return crc ^ 0xFFFFFFFF;
        }
    }
}