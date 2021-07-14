//  Copyright (c) 2021 Demerzel Solutions Limited
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
// 

using DotNetty.Buffers;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Network.P2P.Subprotocols.Eth.V66
{
    public class PooledTransactionsMessageSerializer : IZeroMessageSerializer<PooledTransactionsMessage>
    {
        public void Serialize(IByteBuffer byteBuffer, PooledTransactionsMessage message)
        {
            Eth.V65.PooledTransactionsMessageSerializer ethSerializer = new();
            Rlp ethMessage = new(ethSerializer.Serialize(message.EthMessage));
            int contentLength = Rlp.LengthOf(message.RequestId) + ethMessage.Length;

            int totalLength = Rlp.GetSequenceRlpLength(contentLength);

            RlpStream rlpStream = new NettyRlpStream(byteBuffer);
            byteBuffer.EnsureWritable(totalLength);

            rlpStream.StartSequence(contentLength);
            rlpStream.Encode(message.RequestId);
            rlpStream.Encode(ethMessage);
        }

        public PooledTransactionsMessage Deserialize(IByteBuffer byteBuffer)
        {
            NettyRlpStream rlpStream = new(byteBuffer);
            return Deserialize(rlpStream);
        }
        
        private static PooledTransactionsMessage Deserialize(RlpStream rlpStream)
        {
            PooledTransactionsMessage pooledTransactionsMessage = new();
            rlpStream.ReadSequenceLength();
            pooledTransactionsMessage.RequestId = rlpStream.DecodeLong();
            pooledTransactionsMessage.EthMessage = V65.PooledTransactionsMessageSerializer.Deserialize(rlpStream);
            return pooledTransactionsMessage;
        }
    }
}
