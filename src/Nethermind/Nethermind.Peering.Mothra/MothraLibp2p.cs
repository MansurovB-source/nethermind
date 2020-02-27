﻿//  Copyright (c) 2018 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Nethermind.Peering.Mothra
{
    public class MothraLibp2p : IMothraLibp2p
    {
        private readonly Dictionary<LogLevel, string> _debugLevels = new Dictionary<LogLevel, string>()
        {
            [LogLevel.Trace] = "trace",
            [LogLevel.Debug] = "debug",
            [LogLevel.Information] = "info",
            [LogLevel.Warning] = "warn",
            [LogLevel.Error] = "error",
            [LogLevel.Critical] = "crit"
        };

        private readonly MothraInterop.DiscoveredPeer _discoveredPeer;
        private GCHandle _discoveredPeerHandle;
        private readonly MothraInterop.ReceiveGossip _receiveGossip;
        private GCHandle _receiveGossipHandle;
        private readonly MothraInterop.ReceiveRpc _receiveRpc;
        private GCHandle _receiveRpcHandle;

        public MothraLibp2p()
        {
            unsafe
            {
                _discoveredPeer = DiscoveredPeerHandler;
                _receiveGossip = ReceiveGossipHandler;
                _receiveRpc = ReceiveRpcHandler;
            }

            _discoveredPeerHandle = GCHandle.Alloc(_discoveredPeer);
            _receiveGossipHandle = GCHandle.Alloc(_receiveGossip);
            _receiveRpcHandle = GCHandle.Alloc(_receiveRpc);
        }
        
        public event GossipReceived? GossipReceived;
        
        public event EventHandler<PeerDiscoveredEventArgs>? PeerDiscovered;

        public event EventHandler<RpcReceivedEventArgs>? RpcReceived;

        public void SendGossip(ReadOnlySpan<byte> topicUtf8, ReadOnlySpan<byte> data)
        {
            unsafe
            {
                fixed (byte* topicUtf8Ptr = topicUtf8)
                fixed (byte* dataPtr = data)
                {
                    MothraInterop.SendGossip(topicUtf8Ptr, topicUtf8.Length, dataPtr, data.Length);
                }
            }
        }

        public void SendRpcRequest(ReadOnlySpan<byte> methodUtf8, ReadOnlySpan<byte> peerUtf8, ReadOnlySpan<byte> data)
        {
            unsafe
            {
                fixed (byte* methodUtf8Ptr = methodUtf8)
                fixed (byte* peerUtf8Ptr = peerUtf8)
                fixed (byte* dataPtr = data)
                {
                    MothraInterop.SendRequest(methodUtf8Ptr, methodUtf8.Length, peerUtf8Ptr, peerUtf8.Length, dataPtr,
                        data.Length);
                }
            }
        }

        public void SendRpcResponse(ReadOnlySpan<byte> methodUtf8, ReadOnlySpan<byte> peerUtf8, ReadOnlySpan<byte> data)
        {
            unsafe
            {
                fixed (byte* methodUtf8Ptr = methodUtf8)
                fixed (byte* peerUtf8Ptr = peerUtf8)
                fixed (byte* dataPtr = data)
                {
                    MothraInterop.SendRequest(methodUtf8Ptr, methodUtf8.Length, peerUtf8Ptr, peerUtf8.Length, dataPtr,
                        data.Length);
                }
            }
        }

        public void Start(MothraSettings settings)
        {
            MothraInterop.RegisterHandlers(_discoveredPeer, _receiveGossip, _receiveRpc);
            string[] args = BuildArgs(settings);
            MothraInterop.Start(args, args.Length);
        }

        private string[] BuildArgs(MothraSettings settings)
        {
            List<string> args = new List<string>();
            // Arg0 is the program name (not an arg)
            args.Add("--");

            if (settings.DataDirectory != null)
            {
                args.Add("--datadir");
                args.Add(settings.DataDirectory);
            }

            if (settings.ListenAddress != null)
            {
                args.Add("--listen-address");
                args.Add(settings.ListenAddress);
            }

            if (settings.MaximumPeers != null)
            {
                args.Add("--maxpeers");
                args.Add(settings.MaximumPeers.ToString());
            }

            if (settings.BootNodes.Count > 0)
            {
                args.Add("--boot-nodes");
                string value = string.Join(',', settings.BootNodes);
                args.Add(value);
            }

            if (settings.Port != null)
            {
                args.Add("--port");
                args.Add(settings.Port.ToString());
            }

            if (settings.DiscoveryPort != null)
            {
                args.Add("--discovery-port");
                args.Add(settings.DiscoveryPort.ToString());
            }

            if (settings.DiscoveryAddress != null)
            {
                args.Add("--discovery-address");
                args.Add(settings.DiscoveryAddress);
            }

            if (settings.Topics.Count > 0)
            {
                args.Add("--topics");
                string value = string.Join(',', settings.Topics);
                args.Add(value);
            }

            if (settings.PeerMultiAddresses.Count > 0)
            {
                args.Add("--libp2p-addresses");
                string value = string.Join(',', settings.PeerMultiAddresses);
                args.Add(value);
            }

            if (settings.DebugLevel != null)
            {
                if (_debugLevels.TryGetValue(settings.DebugLevel.Value, out string value))
                {
                    args.Add("--debug-level");
                    args.Add(value);
                }
            }

            if (settings.VerbosityLevel != null)
            {
                args.Add("--verbosity");
                args.Add(settings.VerbosityLevel.ToString());
            }

            return args.ToArray();
        }

        private unsafe void DiscoveredPeerHandler(byte* peerUtf8Ptr, int peerLength)
        {
            byte[] peerUtf8 = new byte[peerLength];
            Marshal.Copy((IntPtr) peerUtf8Ptr, peerUtf8, 0, peerLength);
            OnPeerDiscovered(new PeerDiscoveredEventArgs(peerUtf8));
        }
        
        private void OnPeerDiscovered(PeerDiscoveredEventArgs e)
        {
            PeerDiscovered?.Invoke(this, e);
        }

        private void OnRpcReceived(RpcReceivedEventArgs e)
        {
            RpcReceived?.Invoke(this, e);
        }

        private unsafe void ReceiveGossipHandler(byte* topicUtf8Ptr, int topicLength, byte* dataPtr, int dataLength)
        {
            ReadOnlySpan<byte> topicUtf8 = new ReadOnlySpan<byte>(topicUtf8Ptr, topicLength);
            ReadOnlySpan<byte> data = new ReadOnlySpan<byte>(dataPtr, dataLength);

            // Faster, but non-standard signature
            GossipReceived?.Invoke(topicUtf8, data);
        }

        private unsafe void ReceiveRpcHandler(byte* methodUtf8Ptr, int methodLength, int requestResponseFlag,
            byte* peerUtf8Ptr,
            int peerLength, byte* dataPtr, int dataLength)
        {
            byte[] methodUtf8 = new byte[methodLength];
            Marshal.Copy((IntPtr) methodUtf8Ptr, methodUtf8, 0, methodLength);
            bool isResponse = requestResponseFlag > 0;
            byte[] peerUtf8 = new byte[peerLength];
            Marshal.Copy((IntPtr) peerUtf8Ptr, peerUtf8, 0, peerLength);
            byte[] data = new byte[dataLength];
            Marshal.Copy((IntPtr) dataPtr, data, 0, dataLength);
            OnRpcReceived(new RpcReceivedEventArgs(methodUtf8, isResponse, peerUtf8, data));
        }
    }
}